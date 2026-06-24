using MediatR;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Users.Queries.GetUserTweets;

/// <summary>
/// A user's own tweet timeline (the profile "Tweets" tab): the top-level tweets they authored, newest-first,
/// cursor-paginated. Public — the per-caller like/retweet flags reflect the caller (anonymous ⇒ false).
/// <see cref="Cursor"/> is the opaque token from a previous page's <c>nextCursor</c>; <see cref="Limit"/> is
/// clamped to a sane range by the handler. A 404 surfaces (via the handler) when the handle is unknown.
/// </summary>
public record GetUserTweetsQuery(string Handle, string? Cursor = null, int? Limit = null)
    : IRequest<CursorPage<TweetDto>>;
