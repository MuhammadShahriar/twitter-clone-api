using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;
using TwitterClone.Domain.Enums;

namespace TwitterClone.Infrastructure.Notifications;

/// <summary>
/// Implements the notification creation policy (<see cref="INotificationService"/>) in one place so the
/// four action handlers stay thin and consistent. It only <b>stages</b> the row via
/// <see cref="INotificationRepository"/>; the calling handler's existing <c>SaveChangesAsync</c> commits it
/// in the same transaction as the social action. Handlers call it only on the genuine-new-action path
/// (after their own idempotency short-circuit), so an idempotent re-fire never reaches here.
/// </summary>
public class NotificationService(INotificationRepository notifications) : INotificationService
{
    public async Task CreateAsync(
        Guid recipientId, Guid actorId, NotificationType type, Guid? tweetId, CancellationToken ct = default)
    {
        // (1) Never notify yourself — liking/replying to/retweeting your own tweet creates nothing.
        if (recipientId == actorId)
        {
            return;
        }

        // (2) De-dup unread: if an equivalent unread notification already exists for this
        // (recipient, actor, type, tweet), don't stack another — so e.g. a like→unlike→like cycle leaves a
        // single unread entry. Once the recipient reads it, a fresh genuine action will create a new one.
        if (await notifications.UnreadExistsAsync(recipientId, actorId, type, tweetId, ct))
        {
            return;
        }

        await notifications.AddAsync(new Notification(recipientId, actorId, type, tweetId), ct);
    }
}
