using MediatR;

namespace TwitterClone.Application.Tweets.Commands.UnretweetTweet;

/// <summary>
/// Removes the authenticated caller's retweet of a tweet. <b>Idempotent</b>: unretweeting a tweet the
/// caller has not retweeted is a no-op success. Returns the tweet's updated read model. The API maps a
/// missing tweet to <c>404</c> and a missing token to <c>401</c>.
/// </summary>
public record UnretweetTweetCommand(Guid TweetId) : IRequest<TweetDto>;
