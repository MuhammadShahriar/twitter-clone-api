using TwitterClone.Domain.Common;

namespace TwitterClone.Domain.Entities;

/// <summary>
/// A user's private bookmark of a tweet ("save for later"). Like a <see cref="Like"/>, it references both the
/// user who bookmarked (<see cref="UserId"/>) and the bookmarked tweet (<see cref="TweetId"/>) by plain
/// <see cref="Guid"/> only — the Domain stays free of any auth/Identity dependency and carries no navigations.
/// The FKs (to the Identity user and to the tweet) and the one-bookmark-per-user-per-tweet uniqueness are
/// configured in Infrastructure. A bookmark exists or it does not; there is nothing to mutate, so it is
/// created whole. Unlike a like, a bookmark is <b>private</b>: no public count is exposed and no notification
/// is raised.
/// </summary>
public class Bookmark : BaseEntity
{
    // Parameterless constructor for EF Core materialization only.
    private Bookmark()
    {
    }

    public Bookmark(Guid userId, Guid tweetId)
    {
        UserId = userId;
        TweetId = tweetId;
    }

    /// <summary>The id of the user who bookmarked the tweet (their <c>AspNetUsers.Id</c>).</summary>
    public Guid UserId { get; private set; }

    /// <summary>The id of the tweet that was bookmarked.</summary>
    public Guid TweetId { get; private set; }
}
