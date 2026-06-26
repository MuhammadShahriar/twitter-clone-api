using MediatR;
using Microsoft.AspNetCore.Mvc;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Search.Queries.SearchTweets;
using TwitterClone.Application.Search.Queries.SearchUsers;
using TwitterClone.Application.Tweets;
using TwitterClone.Application.Users;

namespace TwitterClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// People search: users whose handle or display name matches <c>q</c> (case-insensitive), newest-account
    /// first, cursor-paginated. Public; each item carries the caller's <c>isFollowedByCurrentUser</c> flag (so
    /// a signed-in caller can follow from results). A blank <c>q</c> returns an empty page.
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(CursorPage<UserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorPage<UserDto>>> SearchUsers(
        [FromQuery] string? q,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var page = await mediator.Send(new SearchUsersQuery(q, cursor, limit), cancellationToken);
        return Ok(page);
    }

    /// <summary>
    /// Tweet search: tweets whose content matches <c>q</c> (case-insensitive), newest-first, cursor-paginated.
    /// Public; each item carries the caller's like/retweet/bookmark flags. A blank <c>q</c> returns an empty page.
    /// </summary>
    [HttpGet("tweets")]
    [ProducesResponseType(typeof(CursorPage<TweetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorPage<TweetDto>>> SearchTweets(
        [FromQuery] string? q,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var page = await mediator.Send(new SearchTweetsQuery(q, cursor, limit), cancellationToken);
        return Ok(page);
    }
}
