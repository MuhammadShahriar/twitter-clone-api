using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Bookmark-specific repository. The caller's "bookmarked by me" flag shown on a tweet is computed on the
/// read side (a correlated subquery in <c>TweetRepository</c>); this repository exists for the write side —
/// finding a user's existing bookmark so bookmark/un-bookmark can be made idempotent. Bookmarks are private,
/// so (unlike likes) there is no public count to compute.
/// </summary>
public interface IBookmarkRepository : IRepository<Bookmark>
{
    /// <summary>
    /// The given user's bookmark of the given tweet as a <b>tracked</b> entity (so it can be staged for
    /// removal on un-bookmark), or <c>null</c> if the user has not bookmarked that tweet.
    /// </summary>
    Task<Bookmark?> FindAsync(Guid userId, Guid tweetId, CancellationToken ct = default);
}
