using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TwitterClone.Api.Models;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;
using TwitterClone.Application.Tweets.Commands.CreateTweet;
using TwitterClone.Application.Tweets.Commands.DeleteTweet;
using TwitterClone.Application.Tweets.Commands.BookmarkTweet;
using TwitterClone.Application.Tweets.Commands.LikeTweet;
using TwitterClone.Application.Tweets.Commands.RetweetTweet;
using TwitterClone.Application.Tweets.Commands.UnbookmarkTweet;
using TwitterClone.Application.Tweets.Commands.UnlikeTweet;
using TwitterClone.Application.Tweets.Commands.UnretweetTweet;
using TwitterClone.Application.Tweets.Queries.GetReplies;
using TwitterClone.Application.Tweets.Queries.GetTweetById;
using TwitterClone.Application.Tweets.Queries.GetTweets;

namespace TwitterClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TweetsController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// The feed: top-level tweets only, newest first, cursor-paginated. Pass the previous page's
    /// <c>nextCursor</c> to fetch the next page; <c>limit</c> defaults to 20 (max 50).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(CursorPage<TweetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorPage<TweetDto>>> GetTweets(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var page = await mediator.Send(new GetTweetsQuery(cursor, limit), cancellationToken);
        return Ok(page);
    }

    /// <summary>Gets a single tweet by id (with author info and reply count).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TweetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TweetDto>> GetTweetById(Guid id, CancellationToken cancellationToken)
    {
        var tweet = await mediator.Send(new GetTweetByIdQuery(id), cancellationToken);
        return tweet is null ? NotFound() : Ok(tweet);
    }

    /// <summary>
    /// The direct replies to a tweet, oldest first (thread order), cursor-paginated. Public, like the feed.
    /// </summary>
    [HttpGet("{id:guid}/replies")]
    [ProducesResponseType(typeof(CursorPage<TweetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<CursorPage<TweetDto>>> GetReplies(
        Guid id,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var page = await mediator.Send(new GetRepliesQuery(id, cursor, limit), cancellationToken);
        return Ok(page);
    }

    /// <summary>
    /// Creates a tweet (or a reply, when <c>parentId</c> is set), optionally with up to four attached images.
    /// Sent as <c>multipart/form-data</c> (<c>content</c>, optional <c>parentId</c>, optional <c>images</c>
    /// files); the backend uploads any images to Cloudinary then creates the tweet in one call. Requires
    /// authentication; the author is taken from the token. A non-existent <c>parentId</c> or an over-limit /
    /// wrong-type / oversized image fails validation (400).
    /// </summary>
    [HttpPost]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    [ProducesResponseType(typeof(TweetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TweetDto>> CreateTweet(
        [FromForm] CreateTweetRequest request,
        CancellationToken cancellationToken)
    {
        // Read the multipart files into provider-free models so the command (and the whole Application layer)
        // stays free of ASP.NET Core's IFormFile. Validation in the pipeline enforces count/size/type.
        var images = new List<ImageUpload>();
        if (request.Images is { Count: > 0 })
        {
            foreach (var file in request.Images)
            {
                using var buffer = new MemoryStream();
                await file.CopyToAsync(buffer, cancellationToken);
                images.Add(new ImageUpload(file.FileName, file.ContentType, file.Length, buffer.ToArray()));
            }
        }

        var command = new CreateTweetCommand(request.Content ?? string.Empty, request.ParentId, images);
        var tweet = await mediator.Send(command, cancellationToken);

        // Location header points at GET /api/tweets/{id} — the canonical create→read pattern.
        return CreatedAtAction(nameof(GetTweetById), new { id = tweet.Id }, tweet);
    }

    /// <summary>
    /// Deletes a tweet and its direct replies. Requires authentication; only the author may delete their
    /// own tweet (otherwise 403). 204 on success, 404 if the tweet does not exist.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTweet(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTweetCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Likes a tweet as the authenticated caller and returns its updated read model (with the new
    /// <c>likeCount</c> and <c>likedByCurrentUser = true</c>). Idempotent — liking again is a no-op success.
    /// Requires authentication; 404 if the tweet does not exist.
    /// </summary>
    [HttpPost("{id:guid}/like")]
    [Authorize]
    [ProducesResponseType(typeof(TweetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TweetDto>> Like(Guid id, CancellationToken cancellationToken)
    {
        var tweet = await mediator.Send(new LikeTweetCommand(id), cancellationToken);
        return Ok(tweet);
    }

    /// <summary>
    /// Removes the caller's like of a tweet and returns its updated read model (<c>likedByCurrentUser =
    /// false</c>). Idempotent — unliking a tweet you haven't liked is a no-op success. Requires
    /// authentication; 404 if the tweet does not exist.
    /// </summary>
    [HttpDelete("{id:guid}/like")]
    [Authorize]
    [ProducesResponseType(typeof(TweetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TweetDto>> Unlike(Guid id, CancellationToken cancellationToken)
    {
        var tweet = await mediator.Send(new UnlikeTweetCommand(id), cancellationToken);
        return Ok(tweet);
    }

    /// <summary>
    /// Retweets a tweet as the authenticated caller and returns its updated read model (with the new
    /// <c>retweetCount</c> and <c>retweetedByCurrentUser = true</c>). Idempotent — retweeting again is a
    /// no-op success. Requires authentication; 404 if the tweet does not exist.
    /// </summary>
    [HttpPost("{id:guid}/retweet")]
    [Authorize]
    [ProducesResponseType(typeof(TweetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TweetDto>> Retweet(Guid id, CancellationToken cancellationToken)
    {
        var tweet = await mediator.Send(new RetweetTweetCommand(id), cancellationToken);
        return Ok(tweet);
    }

    /// <summary>
    /// Removes the caller's retweet of a tweet and returns its updated read model
    /// (<c>retweetedByCurrentUser = false</c>). Idempotent — unretweeting a tweet you haven't retweeted is a
    /// no-op success. Requires authentication; 404 if the tweet does not exist.
    /// </summary>
    [HttpDelete("{id:guid}/retweet")]
    [Authorize]
    [ProducesResponseType(typeof(TweetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TweetDto>> Unretweet(Guid id, CancellationToken cancellationToken)
    {
        var tweet = await mediator.Send(new UnretweetTweetCommand(id), cancellationToken);
        return Ok(tweet);
    }

    /// <summary>
    /// Bookmarks a tweet as the authenticated caller and returns its updated read model (with
    /// <c>bookmarkedByCurrentUser = true</c>). Bookmarks are private — no count is exposed and no notification
    /// is raised. Idempotent — bookmarking again is a no-op success. Requires authentication; 404 if the tweet
    /// does not exist.
    /// </summary>
    [HttpPost("{id:guid}/bookmark")]
    [Authorize]
    [ProducesResponseType(typeof(TweetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TweetDto>> Bookmark(Guid id, CancellationToken cancellationToken)
    {
        var tweet = await mediator.Send(new BookmarkTweetCommand(id), cancellationToken);
        return Ok(tweet);
    }

    /// <summary>
    /// Removes the caller's bookmark of a tweet and returns its updated read model
    /// (<c>bookmarkedByCurrentUser = false</c>). Idempotent — un-bookmarking a tweet you haven't bookmarked is
    /// a no-op success. Requires authentication; 404 if the tweet does not exist.
    /// </summary>
    [HttpDelete("{id:guid}/bookmark")]
    [Authorize]
    [ProducesResponseType(typeof(TweetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TweetDto>> Unbookmark(Guid id, CancellationToken cancellationToken)
    {
        var tweet = await mediator.Send(new UnbookmarkTweetCommand(id), cancellationToken);
        return Ok(tweet);
    }
}
