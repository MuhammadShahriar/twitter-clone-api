using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TwitterClone.Api.Hubs;

/// <summary>
/// SignalR hub for real-time notification delivery. Push-only: the server sends
/// <see cref="INotificationClient.ReceiveNotification"/> to a recipient's connections; clients call no
/// server methods (the existing REST endpoints cover list / unread-count / mark-read). <c>[Authorize]</c>
/// means a valid JWT is required to connect (the token arrives in the <c>access_token</c> query string —
/// see <c>AddJwtBearerAuthentication</c>), and a connection is associated with its user via
/// <see cref="SubClaimUserIdProvider"/>, so <c>Clients.User(recipientId)</c> targets exactly that person.
/// </summary>
[Authorize]
public class NotificationHub : Hub<INotificationClient>
{
    /// <summary>The route the hub is mapped to (shared by the pipeline mapping and the JWT query-token rule).</summary>
    public const string Path = "/hubs/notifications";
}
