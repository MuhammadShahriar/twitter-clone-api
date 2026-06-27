namespace TwitterClone.Application.Conversations;

/// <summary>
/// One row in the caller's conversation list. <see cref="OtherParticipant"/> is the 1-on-1 counterpart (not
/// the caller); <see cref="LastMessage"/> is a preview of the most recent message (<c>null</c> if none yet);
/// <see cref="UnreadCount"/> is how many messages the caller hasn't read (newer than their last-read time and
/// not sent by them); <see cref="LastMessageAtUtc"/> is the recency sort key.
/// </summary>
public record ConversationDto(
    Guid Id,
    ChatUserDto OtherParticipant,
    LastMessagePreviewDto? LastMessage,
    int UnreadCount,
    DateTime LastMessageAtUtc);

/// <summary>A preview of a conversation's most recent message for the list view.</summary>
public record LastMessagePreviewDto(string ContentPreview, DateTime CreatedAtUtc, Guid SenderId);
