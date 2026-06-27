using MediatR;

namespace TwitterClone.Application.Conversations.Commands.MarkConversationRead;

/// <summary>
/// Marks a conversation read for the caller (sets their <c>LastReadAtUtc</c> to now). Participant-only;
/// returns the conversation's now-zero unread count for the caller.
/// </summary>
public record MarkConversationReadCommand(Guid ConversationId) : IRequest<UnreadConversationsDto>;
