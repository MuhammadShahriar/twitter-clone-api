using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Follow-graph repository. Follower/following counts and the "followed by me" flag shown on a user are
/// computed on the read side (correlated subqueries in <c>UserRepository</c>); this exists for the write
/// side — finding an existing edge so follow/unfollow can be made idempotent.
/// </summary>
public interface IFollowRepository : IRepository<Follow>
{
    /// <summary>
    /// The edge "<paramref name="followerId"/> follows <paramref name="followeeId"/>" as a <b>tracked</b>
    /// entity (so it can be staged for removal on unfollow), or <c>null</c> if no such edge exists.
    /// </summary>
    Task<Follow?> FindAsync(Guid followerId, Guid followeeId, CancellationToken ct = default);
}
