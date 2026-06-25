using TwitterClone.Application.Notifications;

namespace TwitterClone.Api.Hubs;

/// <summary>
/// The strongly-typed set of methods the server can invoke on a connected notification client. A client
/// subscribes to <c>ReceiveNotification</c> to get each new notification (and the updated unread count)
/// pushed the moment it is created. This is the wire contract documented for the 5C frontend.
/// </summary>
public interface INotificationClient
{
    Task ReceiveNotification(NotificationPushDto payload);
}
