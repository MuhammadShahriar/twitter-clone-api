using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Conversations.Commands.MarkConversationRead;

public class MarkConversationReadCommandHandler(
    IConversationRepository conversationRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<MarkConversationReadCommand, UnreadConversationsDto>
{
    public async Task<UnreadConversationsDto> Handle(
        MarkConversationReadCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot mark a conversation read without an authenticated user.");

        // Load my (tracked) participant row so I can update LastReadAtUtc. No row ⇒ either the conversation
        // doesn't exist (404) or I'm not a participant (403) — distinguished without leaking either way.
        var participant = await conversationRepository.GetParticipantTrackedAsync(
            request.ConversationId, userId, cancellationToken);
        if (participant is null)
        {
            if (!await conversationRepository.ExistsAsync(request.ConversationId, cancellationToken))
            {
                throw new NotFoundException(nameof(Conversation), request.ConversationId);
            }

            throw new ForbiddenAccessException();
        }

        participant.MarkReadAt(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Everything up to now is read, so the caller's unread count for this conversation is zero.
        return new UnreadConversationsDto(0);
    }
}
