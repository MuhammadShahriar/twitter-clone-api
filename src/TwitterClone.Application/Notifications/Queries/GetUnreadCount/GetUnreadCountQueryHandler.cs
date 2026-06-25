using MediatR;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Notifications.Queries.GetUnreadCount;

public class GetUnreadCountQueryHandler(
    INotificationRepository notifications, ICurrentUserService currentUser)
    : IRequestHandler<GetUnreadCountQuery, UnreadCountDto>
{
    public async Task<UnreadCountDto> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Notifications require an authenticated user.");

        var count = await notifications.GetUnreadCountAsync(userId, cancellationToken);
        return new UnreadCountDto(count);
    }
}
