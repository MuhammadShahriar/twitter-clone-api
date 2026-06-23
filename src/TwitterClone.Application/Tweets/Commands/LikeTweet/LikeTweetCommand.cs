using MediatR;

namespace TwitterClone.Application.Tweets.Commands.LikeTweet;

/// <summary>
/// Likes a tweet on behalf of the authenticated caller (the user is taken from the token, never the body).
/// <b>Idempotent</b>: liking an already-liked tweet is a no-op success. Returns the tweet's updated read
/// model. The API maps a missing tweet to <c>404</c> and a missing token to <c>401</c>.
/// </summary>
public record LikeTweetCommand(Guid TweetId) : IRequest<TweetDto>;
