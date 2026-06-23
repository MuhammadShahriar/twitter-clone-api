using TwitterClone.Domain.Common;

namespace TwitterClone.Domain.Entities;

/// <summary>
/// A user's "like" of a tweet. Engagement state on a tweet, referencing both the user who liked
/// (<see cref="UserId"/>) and the liked tweet (<see cref="TweetId"/>) by plain <see cref="Guid"/> only —
/// the Domain stays free of any auth/Identity dependency and carries no navigations. The FKs (to the
/// Identity user and to the tweet) and the one-like-per-user-per-tweet uniqueness are configured in
/// Infrastructure. A like exists or it does not; there is nothing to mutate, so it is created whole.
/// </summary>
public class Like : BaseEntity
{
    // Parameterless constructor for EF Core materialization only.
    private Like()
    {
    }

    public Like(Guid userId, Guid tweetId)
    {
        UserId = userId;
        TweetId = tweetId;
    }

    /// <summary>The id of the user who liked the tweet (their <c>AspNetUsers.Id</c>).</summary>
    public Guid UserId { get; private set; }

    /// <summary>The id of the tweet that was liked.</summary>
    public Guid TweetId { get; private set; }
}
