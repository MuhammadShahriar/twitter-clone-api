using MediatR;

namespace TwitterClone.Application.Conversations.Commands.StartConversation;

/// <summary>
/// Get-or-create the 1-on-1 conversation between the caller and a recipient (given by handle or by id).
/// Idempotent: if the conversation already exists it is returned as-is. The caller comes from the token, not
/// the body.
/// </summary>
public record StartConversationCommand(string? RecipientHandle, Guid? RecipientUserId)
    : IRequest<ConversationDto>;
