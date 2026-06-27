using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Conversations.Queries.GetMessages;

public class GetMessagesQueryHandler(
    IConversationRepository conversationRepository,
    IMessageRepository messageRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetMessagesQuery, CursorPage<MessageDto>>
{
    public async Task<CursorPage<MessageDto>> Handle(GetMessagesQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot read messages without an authenticated user.");

        // Participant-only: missing conversation ⇒ 404, otherwise non-participant ⇒ 403 (no content leak).
        if (!await conversationRepository.IsParticipantAsync(request.ConversationId, userId, cancellationToken))
        {
            if (!await conversationRepository.ExistsAsync(request.ConversationId, cancellationToken))
            {
                throw new NotFoundException(nameof(Conversation), request.ConversationId);
            }

            throw new ForbiddenAccessException();
        }

        var limit = PaginationDefaults.Clamp(request.Limit);
        return await messageRepository.GetMessagesAsync(
            request.ConversationId, userId, request.Cursor, limit, cancellationToken);
    }
}
