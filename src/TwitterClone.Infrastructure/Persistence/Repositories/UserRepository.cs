using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Users;

namespace TwitterClone.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-side access to the Identity users for the social features. The projection joins <c>AspNetUsers</c>
/// to the follow graph and returns a plain <see cref="UserDto"/>, so the Identity type never leaks into
/// Application (mirrors how <c>TweetRepository</c> handles tweet reads). Reads are untracked; the
/// follower/following counts are correlated subqueries and the "followed by me" flag comes from the caller.
/// </summary>
public class UserRepository(ApplicationDbContext context) : IUserRepository
{
    public async Task<UserDto?> GetByHandleAsync(string handle, Guid? currentUserId, CancellationToken ct = default) =>
        await context.Users
            .AsNoTracking()
            .Where(u => u.Handle == handle)
            .Select(u => new UserDto(
                u.Id,
                u.Handle,
                u.DisplayName,
                u.Bio,
                u.CreatedAtUtc,
                context.Follows.Count(f => f.FolloweeId == u.Id),
                context.Follows.Count(f => f.FollowerId == u.Id),
                currentUserId != null && context.Follows.Any(f => f.FollowerId == currentUserId && f.FolloweeId == u.Id)))
            .FirstOrDefaultAsync(ct);

    public async Task<Guid?> GetIdByHandleAsync(string handle, CancellationToken ct = default) =>
        await context.Users
            .AsNoTracking()
            .Where(u => u.Handle == handle)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(ct);

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
                // No avatar storage yet — null; the client renders a placeholder.
                (string?)null,
                u.Bio,
                context.Follows.Count(f => f.FolloweeId == u.Id)))
            .ToListAsync(ct);
}
