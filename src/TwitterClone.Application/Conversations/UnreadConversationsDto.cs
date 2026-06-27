namespace TwitterClone.Application.Conversations;

/// <summary>
/// The DM badge count: how many of the caller's conversations contain at least one unread message (a message
/// newer than their last-read time and not sent by them). Also the shape returned by mark-read (then 0 for
/// that conversation). "Conversations with unread" is used rather than a raw message count — it's the
/// simplest, most badge-friendly definition.
/// </summary>
public record UnreadConversationsDto(int UnreadCount);
