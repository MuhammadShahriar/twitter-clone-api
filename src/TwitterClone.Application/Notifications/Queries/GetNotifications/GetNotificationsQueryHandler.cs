using MediatR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;

namespace TwitterClone.Application.Notifications.Queries.GetNotifications;

public class GetNotificationsQueryHandler(
    INotificationRepository notifications, ICurrentUserService currentUser)
    : IRequestHandler<GetNotificationsQuery, CursorPage<NotificationDto>>
{
    public async Task<CursorPage<NotificationDto>> Handle(
        GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive (notifications are personal).
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Notifications require an authenticated user.");

        return await notifications.GetForRecipientAsync(
            userId, request.Cursor, PaginationDefaults.Clamp(request.Limit), cancellationToken);
    }
}
