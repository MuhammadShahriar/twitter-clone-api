using MediatR;

namespace TwitterClone.Application.Tweets.Commands.RetweetTweet;

/// <summary>
/// Retweets a tweet on behalf of the authenticated caller (the user is taken from the token, never the
/// body). <b>Idempotent</b>: retweeting an already-retweeted tweet is a no-op success. Returns the tweet's
/// updated read model. The API maps a missing tweet to <c>404</c> and a missing token to <c>401</c>.
/// </summary>
public record RetweetTweetCommand(Guid TweetId) : IRequest<TweetDto>;
