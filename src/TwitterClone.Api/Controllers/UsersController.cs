using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TwitterClone.Application.Users;
using TwitterClone.Application.Users.Commands.FollowUser;
using TwitterClone.Application.Users.Commands.UnfollowUser;
using TwitterClone.Application.Users.Queries.GetUserByHandle;
using TwitterClone.Application.Users.Queries.GetUserSuggestions;

namespace TwitterClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// "Who to follow" suggestions for the caller: a short list of users they don't already follow,
    /// most-followed first. <c>limit</c> defaults to 5 (max 10). Requires authentication (401).
    /// </summary>
    [HttpGet("suggestions")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<UserSuggestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<UserSuggestionDto>>> GetSuggestions(
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var suggestions = await mediator.Send(new GetUserSuggestionsQuery(limit), cancellationToken);
        return Ok(suggestions);
    }

    /// <summary>
    /// Gets a user's lite public profile by handle (with follower/following counts and, for an
    /// authenticated caller, whether they follow this user). Public; 404 if no such user.
    /// </summary>
    [HttpGet("{handle}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetByHandle(string handle, CancellationToken cancellationToken)
    {
        var user = await mediator.Send(new GetUserByHandleQuery(handle), cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>
    /// Follows a user by handle and returns their updated lite profile (<c>isFollowedByCurrentUser =
    /// true</c>, bumped <c>followerCount</c>). Idempotent — following again is a no-op success. Requires
    /// authentication; 404 if the handle is unknown, 400 if you try to follow yourself.
    /// </summary>
    [HttpPost("{handle}/follow")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Follow(string handle, CancellationToken cancellationToken)
    {
        var user = await mediator.Send(new FollowUserCommand(handle), cancellationToken);
        return Ok(user);
    }

    /// <summary>
    /// Unfollows a user by handle and returns their updated lite profile (<c>isFollowedByCurrentUser =
    /// false</c>). Idempotent — unfollowing someone you don't follow is a no-op success. Requires
    /// authentication; 404 if the handle is unknown.
    /// </summary>
    [HttpDelete("{handle}/follow")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Unfollow(string handle, CancellationToken cancellationToken)
    {
        var user = await mediator.Send(new UnfollowUserCommand(handle), cancellationToken);
        return Ok(user);
    }
}
