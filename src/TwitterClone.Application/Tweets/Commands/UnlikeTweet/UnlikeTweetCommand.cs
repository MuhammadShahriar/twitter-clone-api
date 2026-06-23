using MediatR;

namespace TwitterClone.Application.Tweets.Commands.UnlikeTweet;

/// <summary>
/// Removes the authenticated caller's like of a tweet. <b>Idempotent</b>: unliking a tweet the caller has
/// not liked is a no-op success. Returns the tweet's updated read model. The API maps a missing tweet to
/// <c>404</c> and a missing token to <c>401</c>.
/// </summary>
public record UnlikeTweetCommand(Guid TweetId) : IRequest<TweetDto>;
