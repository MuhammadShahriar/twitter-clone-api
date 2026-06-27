using MediatR;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Conversations.Queries.GetDmUnreadCount;

public class GetDmUnreadCountQueryHandler(
    IConversationRepository conversationRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetDmUnreadCountQuery, UnreadConversationsDto>
{
    public async Task<UnreadConversationsDto> Handle(
        GetDmUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot read the DM unread count without an authenticated user.");

        var count = await conversationRepository.GetUnreadConversationCountAsync(userId, cancellationToken);
        return new UnreadConversationsDto(count);
    }
}
