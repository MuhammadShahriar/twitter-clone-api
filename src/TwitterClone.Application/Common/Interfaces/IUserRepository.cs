using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Users;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Read-side access to users (the Identity <c>AspNetUsers</c> table) for the social features. Like
/// <c>ITweetRepository</c>'s reads, the projection (and the join to the Identity type) lives in
/// Infrastructure, so the Application layer gets back a plain <see cref="UserDto"/> and never sees the
/// Identity type. This is a read/lookup abstraction only — it is not an <c>IRepository&lt;T&gt;</c>
/// (the Identity user is not a Domain entity).
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// The lite profile for the user with the given handle (with follower/following counts and the caller's
    /// "followed by me" flag from <paramref name="currentUserId"/>, null ⇒ false), or <c>null</c> if no such
    /// user exists.
    /// </summary>
    Task<UserDto?> GetByHandleAsync(string handle, Guid? currentUserId, CancellationToken ct = default);

    /// <summary>Resolves a handle to its user id (for the follow/unfollow write side), or <c>null</c> if unknown.</summary>
    Task<Guid?> GetIdByHandleAsync(string handle, CancellationToken ct = default);

    /// <summary>
    /// "Who to follow" suggestions for <paramref name="currentUserId"/>: up to <paramref name="limit"/> users
    /// who are neither the caller nor already followed by them, ordered by follower count (most-followed
    /// first). A single query with correlated counts — no N+1.
    /// </summary>
    Task<IReadOnlyList<UserSuggestionDto>> GetSuggestionsAsync(
        Guid currentUserId, int limit, CancellationToken ct = default);

    /// <summary>
    /// The users who follow <paramref name="userId"/>, most-recently-followed first, cursor-paginated. Each
    /// item is a lite <see cref="UserDto"/> carrying the caller's "followed by me" flag (from
    /// <paramref name="currentUserId"/>, null ⇒ false — so the caller can follow back from the list).
    /// </summary>
    Task<CursorPage<UserDto>> GetFollowersAsync(
        Guid userId, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>
    /// The users <paramref name="userId"/> follows, most-recently-followed first, cursor-paginated. Each item
    /// is a lite <see cref="UserDto"/> carrying the caller's "followed by me" flag (from
    /// <paramref name="currentUserId"/>, null ⇒ false).
    /// </summary>
    Task<CursorPage<UserDto>> GetFollowingAsync(
        Guid userId, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>
    /// Search: users whose handle or display name contains <paramref name="term"/> (case-insensitive),
    /// newest-account first, cursor-paginated. Each item is a lite <see cref="UserDto"/> carrying the caller's
    /// "followed by me" flag (from <paramref name="currentUserId"/>, null ⇒ false — so the caller can follow
    /// from the results).
    /// </summary>
    Task<CursorPage<UserDto>> SearchAsync(
        string term, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default);
}
