using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Feed.Queries.GetFollowingFeed;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// The Following feed: a merged, newest-first timeline of tweets authored or retweeted by the people
    /// the caller follows, cursor-paginated. A retweet entry carries <c>retweetedBy</c>. Pass the previous
    /// page's <c>nextCursor</c> for the next page; <c>limit</c> defaults to 20 (max 50). Requires auth (401).
    /// </summary>
    [HttpGet("following")]
    [Authorize]
    [ProducesResponseType(typeof(CursorPage<TweetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CursorPage<TweetDto>>> Following(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var page = await mediator.Send(new GetFollowingFeedQuery(cursor, limit), cancellationToken);
        return Ok(page);
    }
}
