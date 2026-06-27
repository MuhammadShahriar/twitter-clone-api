namespace TwitterClone.Application.Conversations;

/// <summary>
/// A single message in a conversation thread. <see cref="Sender"/> carries the sender's lite profile (joined
/// in Infrastructure); <see cref="IsMine"/> is <c>true</c> when the caller sent it (so the client can align
/// it right/left without comparing ids itself).
/// </summary>
public record MessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    ChatUserDto Sender,
    string Content,
    DateTime CreatedAtUtc,
    bool IsMine);
