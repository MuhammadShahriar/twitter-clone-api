using TwitterClone.Domain.Common;

namespace TwitterClone.Domain.Entities;

/// <summary>
/// A single text message in a <see cref="Conversation"/>. References its conversation and sender by plain
/// <see cref="Guid"/> only (no navigations — Domain stays Identity-free); FKs/indexes live in Infrastructure.
/// Text only in v1 (no attachments). Immutable once created — there is no edit/delete in 12A.
/// </summary>
public class Message : BaseEntity
{
    /// <summary>Maximum length of a message's text.</summary>
    public const int MaxContentLength = 2000;

    // Parameterless constructor for EF Core materialization only.
    private Message()
    {
    }

    public Message(Guid conversationId, Guid senderId, string content)
    {
        ConversationId = conversationId;
        SenderId = senderId;
        Content = content;
    }

    /// <summary>The conversation this message belongs to.</summary>
    public Guid ConversationId { get; private set; }

    /// <summary>The id of the user who sent this message (their <c>AspNetUsers.Id</c>).</summary>
    public Guid SenderId { get; private set; }

    /// <summary>The message text.</summary>
    public string Content { get; private set; } = string.Empty;
}
