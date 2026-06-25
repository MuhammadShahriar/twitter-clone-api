using TwitterClone.Application.Notifications;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Pushes a freshly-created notification to its recipient's connected clients in real time. This is the
/// Application's framework-free seam for real-time delivery — the concrete transport (SignalR) lives in the
/// API layer, so neither Application nor Infrastructure references it. Called from the single commit
/// chokepoint (the unit of work) <b>after</b> the notification has been committed, so a delivery failure can
/// never roll back the social action that produced it.
/// </summary>
public interface INotificationPublisher
{
    Task PublishAsync(Guid recipientId, NotificationPushDto payload, CancellationToken ct = default);
}
