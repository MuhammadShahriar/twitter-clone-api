using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TwitterClone.Api.Models;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;
using TwitterClone.Application.Users;
using TwitterClone.Application.Users.Commands.FollowUser;
using TwitterClone.Application.Users.Commands.UnfollowUser;
using TwitterClone.Application.Users.Commands.DeleteAvatar;
using TwitterClone.Application.Users.Commands.UpdateAvatar;
using TwitterClone.Application.Users.Commands.UpdateProfile;
using TwitterClone.Application.Users.Queries.GetUserByHandle;
using TwitterClone.Application.Users.Queries.GetUserLikes;
using TwitterClone.Application.Users.Queries.GetUserSuggestions;
using TwitterClone.Application.Users.Queries.GetUserTweets;

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
    /// A user's own tweets (the profile "Tweets" tab): top-level tweets they authored, newest-first,
    /// cursor-paginated. Public, like the main feed; 404 if the handle is unknown.
    /// </summary>
    [HttpGet("{handle}/tweets")]
    [ProducesResponseType(typeof(CursorPage<TweetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CursorPage<TweetDto>>> GetUserTweets(
        string handle,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var page = await mediator.Send(new GetUserTweetsQuery(handle, cursor, limit), cancellationToken);
        return Ok(page);
    }

    /// <summary>
    /// The tweets a user has liked (the profile "Likes" tab), most-recently-liked first, cursor-paginated.
    /// Public; 404 if the handle is unknown.
    /// </summary>
    [HttpGet("{handle}/likes")]
    [ProducesResponseType(typeof(CursorPage<TweetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CursorPage<TweetDto>>> GetUserLikes(
        string handle,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var page = await mediator.Send(new GetUserLikesQuery(handle, cursor, limit), cancellationToken);
        return Ok(page);
    }

    /// <summary>
    /// Updates the authenticated caller's profile (display name + bio) and returns their refreshed lite
    /// profile. Requires authentication (401); over-length fields fail validation (400).
    /// </summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> UpdateProfile(
        [FromBody] UpdateProfileCommand command,
        CancellationToken cancellationToken)
    {
        var user = await mediator.Send(command, cancellationToken);
        return Ok(user);
    }

    /// <summary>
    /// Uploads a new avatar for the authenticated caller (backend-proxied to the image host) and returns
    /// their refreshed lite profile (now with the avatar URL). Sent as <c>multipart/form-data</c> with an
    /// <c>image</c> file. Requires authentication (401); an empty/oversized/wrong-type image fails validation (400).
    /// </summary>
    [HttpPost("me/avatar")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> UpdateAvatar(
        [FromForm] UpdateAvatarRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Image is null)
        {
            return BadRequest("An avatar image is required.");
        }

        // Read the multipart file into a provider-free model so the command stays free of IFormFile.
        using var buffer = new MemoryStream();
        await request.Image.CopyToAsync(buffer, cancellationToken);
        var image = new ImageUpload(
            request.Image.FileName, request.Image.ContentType, request.Image.Length, buffer.ToArray());

        var user = await mediator.Send(new UpdateAvatarCommand(image), cancellationToken);
        return Ok(user);
    }

    /// <summary>
    /// Removes the authenticated caller's avatar and returns their refreshed lite profile (now avatar-less).
    /// Best-effort deletes the old image from the host. Idempotent — removing when there is no avatar still
    /// returns 200. Requires authentication (401).
    /// </summary>
    [HttpDelete("me/avatar")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> DeleteAvatar(CancellationToken cancellationToken)
    {
        var user = await mediator.Send(new DeleteAvatarCommand(), cancellationToken);
        return Ok(user);
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
