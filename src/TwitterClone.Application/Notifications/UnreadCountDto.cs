namespace TwitterClone.Application.Notifications;

/// <summary>The recipient's current unread-notification count (returned by the count and mark-read endpoints).</summary>
public record UnreadCountDto(int UnreadCount);
