using TwitterClone.Domain.Common;

namespace TwitterClone.Domain.Entities;

/// <summary>
/// A user's retweet (re-share) of a tweet. Like <see cref="Like"/>, it is engagement state referencing
/// the retweeting user (<see cref="UserId"/>) and the retweeted tweet (<see cref="TweetId"/>) by plain
/// <see cref="Guid"/> only — no navigations, so the Domain stays Identity-free. The FKs and the
/// one-retweet-per-user-per-tweet uniqueness are configured in Infrastructure. (3A models the retweet as
/// engagement state + counts; surfacing retweets in a feed comes with the Following feed in 3B.)
/// </summary>
public class Retweet : BaseEntity
{
    // Parameterless constructor for EF Core materialization only.
    private Retweet()
    {
    }

    public Retweet(Guid userId, Guid tweetId)
    {
        UserId = userId;
        TweetId = tweetId;
    }

    /// <summary>The id of the user who retweeted (their <c>AspNetUsers.Id</c>).</summary>
    public Guid UserId { get; private set; }

    /// <summary>The id of the tweet that was retweeted.</summary>
    public Guid TweetId { get; private set; }
}
