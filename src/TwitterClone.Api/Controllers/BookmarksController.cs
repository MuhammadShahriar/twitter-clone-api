using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TwitterClone.Application.Bookmarks.Queries.GetBookmarks;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookmarksController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// The authenticated caller's bookmarks, most-recently-bookmarked first, cursor-paginated. Pass the
    /// previous page's <c>nextCursor</c> to fetch the next page; <c>limit</c> defaults to 20 (max 50).
    /// Private — requires authentication (401 without a token); a user only ever sees their own bookmarks.
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(CursorPage<TweetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CursorPage<TweetDto>>> GetBookmarks(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var page = await mediator.Send(new GetBookmarksQuery(cursor, limit), cancellationToken);
        return Ok(page);
    }
}
