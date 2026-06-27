using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Conversations;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Infrastructure.Persistence.Repositories;

/// <summary>
/// Conversation + participant repository. The read projections join the other participant / sender to
/// <c>AspNetUsers</c> here in Infrastructure (so the Identity type never leaks into Application) and compute
/// the last-message preview and the caller's unread count as correlated subqueries — one translatable query,
/// no N+1. The conversation list pages the conversation rows by recency first, then projects just that page
/// (the proven two-step keyset pattern used by the likes/bookmarks timelines).
/// </summary>
public class ConversationRepository(ApplicationDbContext context)
    : Repository<Conversation>(context), IConversationRepository
{
    private const int PreviewLength = 100;

    public async Task<Guid?> GetIdByPairKeyAsync(string pairKey, CancellationToken ct = default) =>
        await Context.Conversations.AsNoTracking()
            .Where(c => c.PairKey == pairKey)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);

    public async Task AddParticipantAsync(ConversationParticipant participant, CancellationToken ct = default) =>
        await Context.ConversationParticipants.AddAsync(participant, ct);

    public async Task<bool> ExistsAsync(Guid conversationId, CancellationToken ct = default) =>
        await Context.Conversations.AsNoTracking().AnyAsync(c => c.Id == conversationId, ct);

    public async Task<bool> IsParticipantAsync(Guid conversationId, Guid userId, CancellationToken ct = default) =>
        await Context.ConversationParticipants.AsNoTracking()
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId, ct);

    public async Task<IReadOnlyList<Guid>> GetParticipantUserIdsAsync(
        Guid conversationId, CancellationToken ct = default) =>
        await Context.ConversationParticipants.AsNoTracking()
            .Where(p => p.ConversationId == conversationId)
            .Select(p => p.UserId)
            .ToListAsync(ct);

    public async Task<Conversation?> GetTrackedAsync(Guid conversationId, CancellationToken ct = default) =>
        await Context.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId, ct);

    public async Task<ConversationParticipant?> GetParticipantTrackedAsync(
        Guid conversationId, Guid userId, CancellationToken ct = default) =>
        await Context.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId, ct);

    public async Task<ConversationDto?> GetDtoAsync(
        Guid conversationId, Guid currentUserId, CancellationToken ct = default) =>
        await ProjectForUser(
                Context.ConversationParticipants.AsNoTracking()
                    .Where(p => p.ConversationId == conversationId && p.UserId == currentUserId),
                currentUserId)
            .FirstOrDefaultAsync(ct);

    public async Task<CursorPage<ConversationDto>> GetConversationsAsync(
        Guid currentUserId, string? cursor, int limit, CancellationToken ct = default)
    {
        var position = TweetCursor.Decode(cursor);

        // Page the conversation rows by recency (keyset over (LastMessageAtUtc, Id)) — ordering happens on the
        // conversation entity, never on a projected member that wraps a subquery. The join to my participation
        // restricts to my conversations.
        var keys = from p in Context.ConversationParticipants.AsNoTracking().Where(p => p.UserId == currentUserId)
                   join c in Context.Conversations.AsNoTracking() on p.ConversationId equals c.Id
                   select new { c.Id, c.LastMessageAtUtc };

        if (position is not null)
        {
            keys = keys.Where(x =>
                x.LastMessageAtUtc < position.CreatedAtUtc
                || (x.LastMessageAtUtc == position.CreatedAtUtc && x.Id.CompareTo(position.Id) < 0));
        }

        var pageKeys = await keys
            .OrderByDescending(x => x.LastMessageAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = pageKeys.Count > limit;
        var page = hasMore ? pageKeys.Take(limit).ToList() : pageKeys;
        var ids = page.Select(x => x.Id).ToList();

        // Load the DTO projections for just this page in one query, then re-stitch in recency order.
        var dtosById = await ProjectForUser(
                Context.ConversationParticipants.AsNoTracking()
                    .Where(p => p.UserId == currentUserId && ids.Contains(p.ConversationId)),
                currentUserId)
            .ToDictionaryAsync(d => d.Id, ct);

        var items = new List<ConversationDto>(page.Count);
        foreach (var key in page)
        {
            if (dtosById.TryGetValue(key.Id, out var dto))
            {
                items.Add(dto);
            }
        }

        var nextCursor = hasMore && page.Count > 0
            ? new TweetCursor(page[^1].LastMessageAtUtc, page[^1].Id).Encode()
            : null;

        return new CursorPage<ConversationDto>(items, nextCursor);
    }

    public async Task<int> GetUnreadConversationCountAsync(Guid currentUserId, CancellationToken ct = default) =>
        await Context.ConversationParticipants.AsNoTracking()
            .Where(p => p.UserId == currentUserId)
            .CountAsync(
                p => Context.Messages.Any(m =>
                    m.ConversationId == p.ConversationId
                    && m.SenderId != currentUserId
                    && (p.LastReadAtUtc == null || m.CreatedAtUtc > p.LastReadAtUtc)),
                ct);

    /// <summary>
    /// Projects each of the caller's participation rows (<paramref name="myParticipations"/>) into a
    /// <see cref="ConversationDto"/>: the OTHER participant (joined to the Identity user), the most-recent
    /// message preview, and the caller's unread count (messages newer than their last-read time, not their
    /// own). The caller's <c>LastReadAtUtc</c> comes from their own participation row <c>p</c>.
    /// </summary>
    private IQueryable<ConversationDto> ProjectForUser(
        IQueryable<ConversationParticipant> myParticipations, Guid currentUserId) =>
        from p in myParticipations
        join c in Context.Conversations.AsNoTracking() on p.ConversationId equals c.Id
        select new ConversationDto(
            c.Id,
            Context.ConversationParticipants
                .Where(op => op.ConversationId == c.Id && op.UserId != currentUserId)
                .Join(Context.Users, op => op.UserId, u => u.Id,
                    (op, u) => new ChatUserDto(u.Id, u.Handle, u.DisplayName, u.AvatarUrl))
                .FirstOrDefault()!,
            Context.Messages
                .Where(m => m.ConversationId == c.Id)
                .OrderByDescending(m => m.CreatedAtUtc)
                .ThenByDescending(m => m.Id)
                .Select(m => new LastMessagePreviewDto(
                    m.Content.Length <= PreviewLength ? m.Content : m.Content.Substring(0, PreviewLength),
                    m.CreatedAtUtc,
                    m.SenderId))
                .FirstOrDefault(),
            Context.Messages.Count(m =>
                m.ConversationId == c.Id
                && m.SenderId != currentUserId
                && (p.LastReadAtUtc == null || m.CreatedAtUtc > p.LastReadAtUtc)),
            c.LastMessageAtUtc);
}
