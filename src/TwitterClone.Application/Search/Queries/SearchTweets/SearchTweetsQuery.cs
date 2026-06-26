using MediatR;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Search.Queries.SearchTweets;

/// <summary>
/// Tweet search: tweets whose content contains <see cref="Q"/> (case-insensitive), newest-first,
/// cursor-paginated. Public — the per-item like/retweet/bookmark flags reflect the caller (anonymous ⇒
/// false). A blank query returns an empty page (no error). <see cref="Cursor"/> is the opaque token from a
/// previous page's <c>nextCursor</c>; <see cref="Limit"/> is clamped to a sane range by the handler.
/// </summary>
public record SearchTweetsQuery(string? Q, string? Cursor = null, int? Limit = null)
    : IRequest<CursorPage<TweetDto>>;
