using TwitterClone.Domain.Enums;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Stages a notification for the recipient as part of a social action. Centralizes the two creation
/// policies so the four action handlers don't each repeat them: (1) <b>never notify yourself</b>
/// (recipient == actor ⇒ nothing is created), and (2) <b>de-duplicate unread</b> — if an equivalent unread
/// notification already exists for the same (recipient, actor, type, tweet), no second one is staged.
/// It only <b>stages</b> the row (no commit); the calling handler's existing
/// <c>SaveChangesAsync</c> commits it atomically with the action itself.
/// </summary>
public interface INotificationService
{
    Task CreateAsync(
        Guid recipientId, Guid actorId, NotificationType type, Guid? tweetId, CancellationToken ct = default);
}
