using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Notifications;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Infrastructure.Persistence;

/// <summary>
/// Unit of Work over the shared <see cref="ApplicationDbContext"/>. Repositories
/// stage their changes on the same scoped context instance; this commits them all
/// in a single transaction.
///
/// Persistence-race exceptions are translated here (the single commit point) into provider-agnostic
/// Application exceptions, so handlers can treat idempotent races as success without the Application layer
/// ever referencing EF Core / Npgsql. Any other failure propagates unchanged.
///
/// This is also the single chokepoint for <b>real-time notification delivery</b> (Module 5B): any
/// notification created during the transaction is captured before the commit and, once the commit
/// succeeds, pushed to its recipient via <see cref="INotificationPublisher"/>. Doing it here (rather than in
/// each of the four social-action handlers) means the push happens exactly once, after the row is durably
/// committed, and the handlers stay free of any real-time concern. Self-skipped / de-duplicated actions
/// never stage a notification row, so they are naturally never pushed.
/// </summary>
public class UnitOfWork(
    ApplicationDbContext context,
    INotificationRepository notifications,
    INotificationPublisher publisher,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Snapshot notifications staged in THIS transaction before they lose their "Added" state on commit.
        var createdNotifications = context.ChangeTracker.Entries<Notification>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => (e.Entity.Id, e.Entity.RecipientId))
            .ToList();

        int result;
        try
        {
            result = await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // A row a write expected to change was already changed/removed by a concurrent write.
            throw new ConcurrencyConflictException(ex.Message, ex);
        }
        catch (DbUpdateException ex) when (DbExceptions.IsUniqueViolation(ex))
        {
            // A concurrent insert of the same row won the race to a unique index.
            throw new UniqueConstraintViolationException(ex.Message, ex);
        }

        // Commit succeeded — now push the freshly-created notifications in real time.
        foreach (var (id, recipientId) in createdNotifications)
        {
            await PublishNotificationAsync(id, recipientId, ct);
        }

        return result;
    }

    private async Task PublishNotificationAsync(Guid id, Guid recipientId, CancellationToken ct)
    {
        // Best-effort: a real-time delivery failure must never fail (or roll back) the action that already
        // committed — at worst the client misses the live event and catches up on its next fetch.
        try
        {
            var dto = await notifications.GetProjectedAsync(id, ct);
            if (dto is null)
            {
                return;
            }

            var unreadCount = await notifications.GetUnreadCountAsync(recipientId, ct);
            await publisher.PublishAsync(recipientId, new NotificationPushDto(dto, unreadCount), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push real-time notification {NotificationId}.", id);
        }
    }
}
