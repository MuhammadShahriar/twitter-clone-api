using TwitterClone.Domain.Common;
using TwitterClone.Domain.Enums;

namespace TwitterClone.Domain.Entities;

/// <summary>
/// A notification delivered to a <b>recipient</b> because another user (the <b>actor</b>) acted on their
/// content — liked/replied to/retweeted one of their tweets, or followed them. Like the other engagement
/// entities (<see cref="Like"/>/<see cref="Follow"/>), it references both users by plain <see cref="Guid"/>
/// with <b>no navigations</b>, so the Domain stays free of any Identity dependency; the FKs/indexes are
/// configured in Infrastructure. <see cref="TweetId"/> is the tweet the action concerns (the liked/replied/
/// retweeted tweet, or the reply itself), and is <c>null</c> for a <see cref="NotificationType.Follow"/>.
/// </summary>
public class Notification : BaseEntity
{
    // Parameterless constructor for EF Core materialization only.
    private Notification()
    {
    }

    public Notification(Guid recipientId, Guid actorId, NotificationType type, Guid? tweetId = null)
    {
        RecipientId = recipientId;
        ActorId = actorId;
        Type = type;
        TweetId = tweetId;
    }

    /// <summary>The user who receives this notification (their <c>AspNetUsers.Id</c>).</summary>
    public Guid RecipientId { get; private set; }

    /// <summary>The user who performed the action that produced this notification.</summary>
    public Guid ActorId { get; private set; }

    /// <summary>Which kind of action produced the notification.</summary>
    public NotificationType Type { get; private set; }

    /// <summary>
    /// The tweet the action concerns — the liked/retweeted tweet, or the reply tweet for a reply.
    /// <c>null</c> for a follow (which has no associated tweet).
    /// </summary>
    public Guid? TweetId { get; private set; }

    /// <summary>Whether the recipient has read this notification. Starts unread.</summary>
    public bool IsRead { get; private set; }

    /// <summary>Marks the notification read (a no-op if already read).</summary>
    public void MarkRead() => IsRead = true;
}
