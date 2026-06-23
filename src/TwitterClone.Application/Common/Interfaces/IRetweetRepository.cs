using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Retweet-specific repository. As with <see cref="ILikeRepository"/>, counts and "retweeted by me" flags
/// are computed on the read side; this exists for the write side — finding a user's existing retweet so
/// retweet/unretweet can be made idempotent.
/// </summary>
public interface IRetweetRepository : IRepository<Retweet>
{
    /// <summary>
    /// The given user's retweet of the given tweet as a <b>tracked</b> entity (so it can be staged for
    /// removal on unretweet), or <c>null</c> if the user has not retweeted that tweet.
    /// </summary>
    Task<Retweet?> FindAsync(Guid userId, Guid tweetId, CancellationToken ct = default);
}
