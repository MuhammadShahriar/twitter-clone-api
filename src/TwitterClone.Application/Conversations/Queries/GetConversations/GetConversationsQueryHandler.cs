using MediatR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;

namespace TwitterClone.Application.Conversations.Queries.GetConversations;

public class GetConversationsQueryHandler(
    IConversationRepository conversationRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetConversationsQuery, CursorPage<ConversationDto>>
{
    public async Task<CursorPage<ConversationDto>> Handle(
        GetConversationsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot list conversations without an authenticated user.");

        var limit = PaginationDefaults.Clamp(request.Limit);
        return await conversationRepository.GetConversationsAsync(userId, request.Cursor, limit, cancellationToken);
    }
}
