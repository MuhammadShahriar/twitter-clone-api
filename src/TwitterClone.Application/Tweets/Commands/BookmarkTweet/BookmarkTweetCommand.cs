using MediatR;

namespace TwitterClone.Application.Tweets.Commands.BookmarkTweet;

/// <summary>
/// Bookmarks a tweet on behalf of the authenticated caller (the user is taken from the token, never the body).
/// <b>Idempotent</b>: bookmarking an already-bookmarked tweet is a no-op success. Bookmarks are private — no
/// notification is raised and no public count changes. Returns the tweet's updated read model (with
/// <c>bookmarkedByCurrentUser = true</c>). The API maps a missing tweet to <c>404</c> and a missing token to
/// <c>401</c>.
/// </summary>
public record BookmarkTweetCommand(Guid TweetId) : IRequest<TweetDto>;
