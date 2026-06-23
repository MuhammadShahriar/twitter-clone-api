using MediatR;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Tweets.Queries.GetTweets;

/// <summary>
/// The main feed: top-level tweets only, newest-first, cursor-paginated. <see cref="Cursor"/> is the
/// opaque token from a previous page's <c>nextCursor</c> (null for the first page); <see cref="Limit"/>
/// is the requested page size (clamped to a sane range by the handler).
/// </summary>
public record GetTweetsQuery(string? Cursor = null, int? Limit = null) : IRequest<CursorPage<TweetDto>>;
