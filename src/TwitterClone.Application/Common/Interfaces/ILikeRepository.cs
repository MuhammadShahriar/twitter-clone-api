using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Like-specific repository. The counts and "liked by me" flags shown on a tweet are computed on the
/// read side (correlated subqueries in <c>TweetRepository</c>); this repository exists for the write side
/// — finding a user's existing like so like/unlike can be made idempotent.
/// </summary>
public interface ILikeRepository : IRepository<Like>
{
    /// <summary>
    /// The given user's like of the given tweet as a <b>tracked</b> entity (so it can be staged for
    /// removal on unlike), or <c>null</c> if the user has not liked that tweet.
    /// </summary>
    Task<Like?> FindAsync(Guid userId, Guid tweetId, CancellationToken ct = default);
}
