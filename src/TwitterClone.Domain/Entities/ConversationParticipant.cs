namespace TwitterClone.Domain.Entities;

/// <summary>
/// A user's membership in a <see cref="Conversation"/>, plus their per-participant read state. Has a
/// <b>composite key</b> <c>(ConversationId, UserId)</c> rather than a single id (so it is not a
/// <see cref="Common.BaseEntity"/>), keeping read state per-member and leaving room for group conversations
/// later. References the conversation and the user by plain <see cref="Guid"/>; FKs live in Infrastructure.
/// </summary>
public class ConversationParticipant
{
    // Parameterless constructor for EF Core materialization only.
    private ConversationParticipant()
    {
    }

    public ConversationParticipant(Guid conversationId, Guid userId)
    {
        ConversationId = conversationId;
        UserId = userId;
    }

    /// <summary>The conversation this membership belongs to.</summary>
    public Guid ConversationId { get; private set; }

    /// <summary>The member's user id (their <c>AspNetUsers.Id</c>).</summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// When this member last read the conversation (UTC), or <c>null</c> if they never have. Messages newer
    /// than this (and not sent by them) are unread. Updated via <see cref="MarkReadAt"/>.
    /// </summary>
    public DateTime? LastReadAtUtc { get; private set; }

    /// <summary>Marks the conversation read for this member up to <paramref name="at"/>.</summary>
    public void MarkReadAt(DateTime at) => LastReadAtUtc = at;
}
