using TwitterClone.Domain.Common;

namespace TwitterClone.Domain.Entities;

/// <summary>
/// A directed "follow" edge in the social graph: <see cref="FollowerId"/> follows <see cref="FolloweeId"/>.
/// Both ends reference users by plain <see cref="Guid"/> (their <c>AspNetUsers.Id</c>) only — no
/// navigations, so the Domain stays Identity-free (consistent with <see cref="Like"/>/<see cref="Retweet"/>).
/// The FKs to the user table and the one-edge-per-pair uniqueness are configured in Infrastructure. A
/// follow exists or it does not; there is nothing to mutate, so it is created whole.
/// </summary>
public class Follow : BaseEntity
{
    // Parameterless constructor for EF Core materialization only.
    private Follow()
    {
    }

    public Follow(Guid followerId, Guid followeeId)
    {
        FollowerId = followerId;
        FolloweeId = followeeId;
    }

    /// <summary>The id of the user doing the following.</summary>
    public Guid FollowerId { get; private set; }

    /// <summary>The id of the user being followed.</summary>
    public Guid FolloweeId { get; private set; }
}
