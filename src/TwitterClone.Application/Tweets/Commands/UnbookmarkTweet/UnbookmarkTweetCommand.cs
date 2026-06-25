using MediatR;

namespace TwitterClone.Application.Tweets.Commands.UnbookmarkTweet;

/// <summary>
/// Removes the authenticated caller's bookmark of a tweet. <b>Idempotent</b>: un-bookmarking a tweet the
/// caller has not bookmarked is a no-op success. Returns the tweet's updated read model (with
/// <c>bookmarkedByCurrentUser = false</c>). The API maps a missing tweet to <c>404</c> and a missing token to
/// <c>401</c>.
/// </summary>
public record UnbookmarkTweetCommand(Guid TweetId) : IRequest<TweetDto>;
