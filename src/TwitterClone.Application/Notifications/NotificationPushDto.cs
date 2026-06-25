namespace TwitterClone.Application.Notifications;

/// <summary>
/// The payload pushed to a recipient's connected clients in real time when a notification is created:
/// the new <see cref="NotificationDto"/> plus the recipient's resulting unread count (so the client can
/// update both the list and the bell badge from a single event, without a follow-up fetch).
/// </summary>
public record NotificationPushDto(NotificationDto Notification, int UnreadCount);
