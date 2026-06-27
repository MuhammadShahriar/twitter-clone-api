using MediatR;

namespace TwitterClone.Application.Conversations.Commands.SendMessage;

/// <summary>
/// Sends a text message to a conversation. The sender is the authenticated caller (from the token), who must
/// be a participant of the conversation; the handler bumps the conversation's recency.
/// </summary>
public record SendMessageCommand(Guid ConversationId, string Content) : IRequest<MessageDto>;
