namespace TwitterClone.Application.Conversations;

/// <summary>
/// The payload pushed to a recipient's connected clients in real time when a message is sent: the new
/// <see cref="MessageDto"/>, the conversation it belongs to, and the recipient's resulting DM unread count
/// (so the client can drop the message into the open thread and bump the DM badge from a single event,
/// without a follow-up fetch). <see cref="ConversationId"/> is surfaced at the top level (it also lives on
/// the message) so a client can route the event without reading into the nested DTO.
/// </summary>
public record MessagePushDto(MessageDto Message, Guid ConversationId, int UnreadCount);
