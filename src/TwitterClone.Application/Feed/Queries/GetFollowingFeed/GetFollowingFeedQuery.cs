using MediatR;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Feed.Queries.GetFollowingFeed;

/// <summary>
/// The Following feed: a merged, newest-first timeline of tweets authored or retweeted by the people the
/// authenticated caller follows, cursor-paginated. <see cref="Cursor"/>/<see cref="Limit"/> behave as on
/// the main feed query. Requires an authenticated caller (the API maps a missing token to <c>401</c>).
/// </summary>
public record GetFollowingFeedQuery(string? Cursor = null, int? Limit = null) : IRequest<CursorPage<TweetDto>>;
