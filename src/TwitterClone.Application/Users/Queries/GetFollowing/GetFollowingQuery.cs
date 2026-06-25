using MediatR;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Users;

namespace TwitterClone.Application.Users.Queries.GetFollowing;

/// <summary>
/// The list of users <see cref="Handle"/> follows, most-recently-followed first, cursor-paginated. Public —
/// the per-item <c>isFollowedByCurrentUser</c> flag reflects the caller (anonymous ⇒ false), so a signed-in
/// caller can follow back from the list. <see cref="Cursor"/> is the opaque token from a previous page's
/// <c>nextCursor</c>; <see cref="Limit"/> is clamped to a sane range by the handler. A 404 surfaces (via the
/// handler) when the handle is unknown.
/// </summary>
public record GetFollowingQuery(string Handle, string? Cursor = null, int? Limit = null)
    : IRequest<CursorPage<UserDto>>;
