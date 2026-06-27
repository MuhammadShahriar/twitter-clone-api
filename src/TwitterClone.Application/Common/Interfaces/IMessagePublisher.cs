using TwitterClone.Application.Conversations;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Pushes a freshly-sent direct message to its recipient's connected clients in real time. The Application's
/// framework-free seam for DM delivery — the concrete transport (SignalR) lives in the API layer, so neither
/// Application nor Infrastructure references it (exactly like <see cref="INotificationPublisher"/>). Called
/// from the single commit chokepoint (the unit of work) <b>after</b> the message has been committed, so a
/// delivery failure can never roll back the send.
/// </summary>
public interface IMessagePublisher
{
    Task PublishAsync(Guid recipientId, MessagePushDto payload, CancellationToken ct = default);
}
