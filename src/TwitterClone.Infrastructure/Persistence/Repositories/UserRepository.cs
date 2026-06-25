using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Users;
using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Identity;

namespace TwitterClone.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-side access to the Identity users for the social features. The projection joins <c>AspNetUsers</c>
/// to the follow graph and returns a plain <see cref="UserDto"/>, so the Identity type never leaks into
/// Application (mirrors how <c>TweetRepository</c> handles tweet reads). Reads are untracked; the
/// follower/following counts are correlated subqueries and the "followed by me" flag comes from the caller.
/// </summary>
public class UserRepository(ApplicationDbContext context) : IUserRepository
{
    public async Task<UserDto?> GetByHandleAsync(string handle, Guid? currentUserId, CancellationToken ct = default)
    {
        var normalizedHandle = HandleNormalizer.Normalize(handle);
        return await context.Users
            .AsNoTracking()
            .Where(u => u.NormalizedHandle == normalizedHandle)
            .Select(u => new UserDto(
                u.Id,
                u.Handle,
                u.DisplayName,
                u.Bio,
                u.AvatarUrl,
                u.CreatedAtUtc,
                context.Follows.Count(f => f.FolloweeId == u.Id),
                context.Follows.Count(f => f.FollowerId == u.Id),
                currentUserId != null && context.Follows.Any(f => f.FollowerId == currentUserId && f.FolloweeId == u.Id)))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Guid?> GetIdByHandleAsync(string handle, CancellationToken ct = default)
    {
        var normalizedHandle = HandleNormalizer.Normalize(handle);
        return await context.Users
            .AsNoTracking()
            .Where(u => u.NormalizedHandle == normalizedHandle)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<UserSuggestionDto>> GetSuggestionsAsync(
        Guid currentUserId, int limit, CancellationToken ct = default) =>
        await context.Users
            .AsNoTracking()
            // Exclude the caller and anyone they already follow. The follow check is a single NOT EXISTS,
            // and the follower count is a correlated subquery — one query, no N+1.
            .Where(u => u.Id != currentUserId
                && !context.Follows.Any(f => f.FollowerId == currentUserId && f.FolloweeId == u.Id))
            // Order BEFORE projecting: ordering must be over the correlated count subquery on the entity, not
            // over a projected DTO member (EF can't translate ORDER BY over a projection that wraps a
            // subquery). Most-followed first, with Id as a stable tiebreaker for equal-count rows. Take, then
            // project — so the whole thing stays one translatable SQL query (no client-side evaluation).
            // Order BEFORE projecting: ordering must be over the correlated count subquery on the entity, not
            // over a projected DTO member (EF can't translate ORDER BY over a projection that wraps a
            // subquery). Most-followed first, with Id as a stable tiebreaker for equal-count rows. Take, then
            // project — so the whole thing stays one translatable SQL query (no client-side evaluation).
            .OrderByDescending(u => context.Follows.Count(f => f.FolloweeId == u.Id))
            .ThenByDescending(u => u.Id)
            .Take(limit)
            .Select(u => new UserSuggestionDto(
                u.Id,
                u.Handle,
                u.DisplayName,
                u.AvatarUrl,
                u.Bio,
                context.Follows.Count(f => f.FolloweeId == u.Id)))
            .ToListAsync(ct);

    public async Task<CursorPage<UserDto>> GetFollowersAsync(
        Guid userId, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default)
    {
        // Followers of userId = edges pointing AT them; the follower is the edge's FollowerId.
        var pageEdges = await PageEdges(
                context.Follows.AsNoTracking().Where(f => f.FolloweeId == userId), cursor, limit)
            .Select(f => new FollowEdge(f.Id, f.CreatedAtUtc, f.FollowerId))
            .ToListAsync(ct);

        return await BuildUserPageAsync(pageEdges, currentUserId, limit, ct);
    }

    public async Task<CursorPage<UserDto>> GetFollowingAsync(
        Guid userId, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default)
    {
        // Who userId follows = edges going FROM them; the followee is the edge's FolloweeId.
        var pageEdges = await PageEdges(
                context.Follows.AsNoTracking().Where(f => f.FollowerId == userId), cursor, limit)
            .Select(f => new FollowEdge(f.Id, f.CreatedAtUtc, f.FolloweeId))
            .ToListAsync(ct);

        return await BuildUserPageAsync(pageEdges, currentUserId, limit, ct);
    }

    /// <summary>
    /// Applies the keyset cursor predicate (strictly older than the last edge seen, with the edge's <c>Id</c>
    /// as a stable tiebreaker) and over-fetches <c>limit + 1</c> follow edges newest-first. Ordering and the
    /// cursor key are the edge's <c>(CreatedAtUtc, Id)</c> — the same keyset shape the feeds/likes use.
    /// </summary>
    private static IQueryable<Follow> PageEdges(IQueryable<Follow> edges, string? cursor, int limit)
    {
        var position = TweetCursor.Decode(cursor);
        if (position is not null)
        {
            edges = edges.Where(f =>
                f.CreatedAtUtc < position.CreatedAtUtc
                || (f.CreatedAtUtc == position.CreatedAtUtc && f.Id.CompareTo(position.Id) < 0));
        }

        return edges
            .OrderByDescending(f => f.CreatedAtUtc)
            .ThenByDescending(f => f.Id)
            .Take(limit + 1);
    }

    /// <summary>
    /// Loads the lite <see cref="UserDto"/> projections for a page of follow edges in one query (follower /
    /// following counts as correlated subqueries, the caller's "followed by me" flag from
    /// <paramref name="currentUserId"/>) and re-stitches them into edge order — mirroring how the likes
    /// timeline pages a derived ordering it can't project through. A user removed between the two queries is
    /// simply skipped. The cursor is built from the last edge actually returned.
    /// </summary>
    private async Task<CursorPage<UserDto>> BuildUserPageAsync(
        List<FollowEdge> pageEdges, Guid? currentUserId, int limit, CancellationToken ct)
    {
        var hasMore = pageEdges.Count > limit;
        var page = hasMore ? pageEdges.Take(limit).ToList() : pageEdges;
        var userIds = page.Select(e => e.OtherUserId).ToList();

        var usersById = await context.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new UserDto(
                u.Id,
                u.Handle,
                u.DisplayName,
                u.Bio,
                u.AvatarUrl,
                u.CreatedAtUtc,
                context.Follows.Count(f => f.FolloweeId == u.Id),
                context.Follows.Count(f => f.FollowerId == u.Id),
                currentUserId != null && context.Follows.Any(f => f.FollowerId == currentUserId && f.FolloweeId == u.Id)))
            .ToDictionaryAsync(u => u.Id, ct);

        var items = new List<UserDto>(page.Count);
        foreach (var edge in page)
        {
            if (usersById.TryGetValue(edge.OtherUserId, out var dto))
            {
                items.Add(dto);
            }
        }

        var nextCursor = hasMore && page.Count > 0
            ? new TweetCursor(page[^1].CreatedAtUtc, page[^1].EdgeId).Encode()
            : null;

        return new CursorPage<UserDto>(items, nextCursor);
    }

    /// <summary>A paged follow edge: its sort key (the edge's id + time) and the user on the other end.</summary>
    private sealed record FollowEdge(Guid EdgeId, DateTime CreatedAtUtc, Guid OtherUserId);
}
