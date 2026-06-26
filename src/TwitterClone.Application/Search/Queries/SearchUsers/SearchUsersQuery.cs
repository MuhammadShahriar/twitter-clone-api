using MediatR;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Users;

namespace TwitterClone.Application.Search.Queries.SearchUsers;

/// <summary>
/// People search: users whose handle or display name contains <see cref="Q"/> (case-insensitive),
/// newest-account first, cursor-paginated. Public — the per-item <c>isFollowedByCurrentUser</c> flag reflects
/// the caller (anonymous ⇒ false), so a signed-in caller can follow from the results. A blank query returns an
/// empty page (no error). <see cref="Cursor"/> is the opaque token from a previous page's <c>nextCursor</c>;
/// <see cref="Limit"/> is clamped to a sane range by the handler.
/// </summary>
public record SearchUsersQuery(string? Q, string? Cursor = null, int? Limit = null)
    : IRequest<CursorPage<UserDto>>;
