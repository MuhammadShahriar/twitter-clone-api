using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Notifications;
using TwitterClone.Domain.Entities;
using TwitterClone.Domain.Enums;

namespace TwitterClone.Infrastructure.Persistence.Repositories;

/// <summary>
/// Notification repository. Inherits the generic staging operations and adds the read queries, which join
/// each notification to its actor in <c>AspNetUsers</c> and project straight into
/// <see cref="NotificationDto"/> (the Identity join lives here, in Infrastructure, so it never leaks into
/// Application — mirroring <c>TweetRepository</c>). Reads are untracked, ordering is pushed to the database,
/// and the list is cursor-paginated over the same <c>(CreatedAtUtc, Id)</c> keyset as the tweet feeds.
/// </summary>
public class NotificationRepository(ApplicationDbContext context)
    : Repository<Notification>(context), INotificationRepository
{
    // The tweet-text snippet is truncated in SQL so a long tweet isn't fetched in full just to preview it.
    private const int PreviewLength = 100;

    public async Task<bool> UnreadExistsAsync(
        Guid recipientId, Guid actorId, NotificationType type, Guid? tweetId, CancellationToken ct = default) =>
        await Set.AsNoTracking().AnyAsync(
            n => !n.IsRead
                 && n.RecipientId == recipientId
                 && n.ActorId == actorId
                 && n.Type == type
                 // EF's default (C#) null semantics translate this to "TweetId IS NULL" when tweetId is null,
                 // so a follow (null tweet) de-dups correctly against other unread follows.
                 && n.TweetId == tweetId,
            ct);

    public async Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken ct = default) =>
        await Set.AsNoTracking().CountAsync(n => n.RecipientId == recipientId && !n.IsRead, ct);

    public async Task<IReadOnlyList<Notification>> GetUnreadAsync(Guid recipientId, CancellationToken ct = default) =>
        // Tracked (no AsNoTracking) so the mark-all-read handler can flip IsRead and have the UoW persist it.
        await Set.Where(n => n.RecipientId == recipientId && !n.IsRead).ToListAsync(ct);

    public async Task<CursorPage<NotificationDto>> GetForRecipientAsync(
        Guid recipientId, string? cursor, int limit, CancellationToken ct = default)
    {
        var position = TweetCursor.Decode(cursor);

        // Newest-first; the cursor predicate is "strictly older than the last item seen", with Id as a
        // tiebreaker so rows sharing a CreatedAtUtc page deterministically (same keyset as the main feed).
        var query = Set.AsNoTracking().Where(n => n.RecipientId == recipientId);
        if (position is not null)
        {
            query = query.Where(n =>
                n.CreatedAtUtc < position.CreatedAtUtc
                || (n.CreatedAtUtc == position.CreatedAtUtc && n.Id.CompareTo(position.Id) < 0));
        }

        var rows = await Project(query
                .OrderByDescending(n => n.CreatedAtUtc)
                .ThenByDescending(n => n.Id))
            .Take(limit + 1)
            .ToListAsync(ct);

        if (rows.Count <= limit)
        {
            return new CursorPage<NotificationDto>(rows, null);
        }

        var page = rows.Take(limit).ToList();
        var last = page[^1];
        var nextCursor = new TweetCursor(last.CreatedAtUtc, last.Id).Encode();
        return new CursorPage<NotificationDto>(page, nextCursor);
    }

    public async Task<NotificationDto?> GetProjectedAsync(Guid id, CancellationToken ct = default) =>
        await Project(Set.AsNoTracking().Where(n => n.Id == id)).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Joins each notification to its actor and (left-join) to the associated tweet, projecting into
    /// <see cref="NotificationDto"/>. The Identity join is confined here; the tweet preview is truncated
    /// in SQL and is <c>null</c> when there is no associated tweet (a follow) or the tweet was deleted.
    /// </summary>
    private IQueryable<NotificationDto> Project(IQueryable<Notification> notifications) =>
        from n in notifications
        join actor in Context.Users on n.ActorId equals actor.Id
        from tweet in Context.Tweets.Where(t => t.Id == n.TweetId).DefaultIfEmpty()
        select new NotificationDto(
            n.Id,
            new NotificationActorDto(actor.Handle, actor.DisplayName, actor.AvatarUrl),
            n.Type,
            n.IsRead,
            n.CreatedAtUtc,
            n.TweetId,
            tweet == null
                ? null
                : tweet.Content.Length <= PreviewLength
                    ? tweet.Content
                    : tweet.Content.Substring(0, PreviewLength));
}
