using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Notifications;
using TwitterClone.Domain.Entities;
using TwitterClone.Domain.Enums;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Notification repository: the write side stages new rows (via the generic <see cref="IRepository{T}"/>)
/// and the read side projects them — joined to the actor in <c>AspNetUsers</c> — into
/// <see cref="NotificationDto"/>, with the Identity join confined to Infrastructure (mirroring
/// <c>TweetRepository</c>/<c>UserRepository</c>).
/// </summary>
public interface INotificationRepository : IRepository<Notification>
{
    /// <summary>
    /// Whether an <b>unread</b> notification already exists for this exact
    /// (recipient, actor, type, tweet) tuple — used to avoid stacking duplicates (e.g. a
    /// like→unlike→like cycle). <paramref name="tweetId"/> is <c>null</c> for a follow and is matched as such.
    /// </summary>
    Task<bool> UnreadExistsAsync(
        Guid recipientId, Guid actorId, NotificationType type, Guid? tweetId, CancellationToken ct = default);

    /// <summary>The recipient's notifications, newest-first, cursor-paginated.</summary>
    Task<CursorPage<NotificationDto>> GetForRecipientAsync(
        Guid recipientId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>
    /// A single notification projected to its <see cref="NotificationDto"/> (actor join + tweet preview),
    /// or <c>null</c> if it no longer exists. Used to build the real-time push payload after commit.
    /// </summary>
    Task<NotificationDto?> GetProjectedAsync(Guid id, CancellationToken ct = default);

    /// <summary>How many of the recipient's notifications are unread.</summary>
    Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken ct = default);

    /// <summary>
    /// The recipient's unread notifications as <b>tracked</b> entities, so the mark-all-read use case can
    /// flip them read and have the unit of work commit the change.
    /// </summary>
    Task<IReadOnlyList<Notification>> GetUnreadAsync(Guid recipientId, CancellationToken ct = default);
}
