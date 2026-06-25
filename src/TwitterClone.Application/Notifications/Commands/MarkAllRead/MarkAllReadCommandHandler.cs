using MediatR;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Notifications.Commands.MarkAllRead;

public class MarkAllReadCommandHandler(
    INotificationRepository notifications, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    : IRequestHandler<MarkAllReadCommand, UnreadCountDto>
{
    public async Task<UnreadCountDto> Handle(MarkAllReadCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Notifications require an authenticated user.");

        // Load the caller's unread notifications (tracked), flip each read, and let the unit of work commit.
        // Done via tracked entities + SaveChanges (not a bulk ExecuteUpdate) so it works on the in-memory
        // provider too and stays consistent with the repository + unit-of-work pattern.
        var unread = await notifications.GetUnreadAsync(userId, cancellationToken);
        foreach (var notification in unread)
        {
            notification.MarkRead();
        }

        if (unread.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Everything is now read; the new unread count is zero.
        return new UnreadCountDto(0);
    }
}
