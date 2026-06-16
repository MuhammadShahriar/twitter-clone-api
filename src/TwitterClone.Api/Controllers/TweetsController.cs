using MediatR;
using Microsoft.AspNetCore.Mvc;
using TwitterClone.Application.Tweets;
using TwitterClone.Application.Tweets.Commands.CreateTweet;
using TwitterClone.Application.Tweets.Queries.GetTweetById;
using TwitterClone.Application.Tweets.Queries.GetTweets;

namespace TwitterClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TweetsController(ISender mediator) : ControllerBase
{
    /// <summary>Lists all tweets, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TweetDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TweetDto>>> GetTweets(CancellationToken cancellationToken)
    {
        var tweets = await mediator.Send(new GetTweetsQuery(), cancellationToken);
        return Ok(tweets);
    }

    /// <summary>Gets a single tweet by id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TweetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TweetDto>> GetTweetById(Guid id, CancellationToken cancellationToken)
    {
        var tweet = await mediator.Send(new GetTweetByIdQuery(id), cancellationToken);
        return tweet is null ? NotFound() : Ok(tweet);
    }

    /// <summary>Creates a new tweet.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TweetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TweetDto>> CreateTweet(
        [FromBody] CreateTweetCommand command,
        CancellationToken cancellationToken)
    {
        var tweet = await mediator.Send(command, cancellationToken);

        // Location header points at GET /api/tweets/{id} — the canonical create→read pattern.
        return CreatedAtAction(nameof(GetTweetById), new { id = tweet.Id }, tweet);
    }
}
