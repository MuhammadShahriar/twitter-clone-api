using TwitterClone.Domain.Enums;

namespace TwitterClone.Application.Notifications;

/// <summary>
/// A single notification as returned by <c>GET /api/notifications</c>. The <see cref="Actor"/> fields come
/// from a read-side join to the Identity user table (in Infrastructure), so the Application never sees the
/// Identity type. <see cref="TweetPreview"/> is a short snippet of the associated tweet's text (or
/// <c>null</c> when there is no tweet, e.g. a follow) — kept short on the read side rather than over-fetched.
/// </summary>
public record NotificationDto(
    Guid Id,
    NotificationActorDto Actor,
    NotificationType Type,
    bool IsRead,
    DateTime CreatedAtUtc,
    Guid? TweetId,
    string? TweetPreview);

/// <summary>The user who triggered a notification, projected for display (handle/name/avatar only).</summary>
public record NotificationActorDto(string Handle, string DisplayName, string? AvatarUrl);
