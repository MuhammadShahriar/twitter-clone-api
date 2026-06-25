using Microsoft.AspNetCore.SignalR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Notifications;

namespace TwitterClone.Api.Hubs;

/// <summary>
/// SignalR implementation of <see cref="INotificationPublisher"/>: delivers a notification to all of the
/// recipient's live connections via the typed hub. Lives in the API layer (SignalR is an ASP.NET Core
/// concern) so the Application/Infrastructure layers stay transport-agnostic. Targeting by user id (not
/// connection) means every device the recipient has open receives the push; if they have none, it is a
/// harmless no-op.
/// </summary>
public class NotificationHubPublisher(IHubContext<NotificationHub, INotificationClient> hub)
    : INotificationPublisher
{
    public Task PublishAsync(Guid recipientId, NotificationPushDto payload, CancellationToken ct = default) =>
        hub.Clients.User(recipientId.ToString()).ReceiveNotification(payload);
}
