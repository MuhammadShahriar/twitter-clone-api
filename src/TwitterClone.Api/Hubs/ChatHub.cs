using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TwitterClone.Api.Hubs;

/// <summary>
/// SignalR hub for real-time direct-message delivery. Push-only (like <see cref="NotificationHub"/>): the
/// server sends <see cref="IChatClient.ReceiveMessage"/> to a recipient's connections; clients call no server
/// methods (the 12A REST endpoints cover start / send / list / read). <c>[Authorize]</c> requires a valid
/// JWT to connect — the token arrives in the <c>access_token</c> query string, accepted for this hub path by
/// the same rule that serves <see cref="NotificationHub"/> (see <c>AddJwtBearerAuthentication</c>) — and a
/// connection is keyed to its user via <see cref="SubClaimUserIdProvider"/>, so <c>Clients.User(recipientId)</c>
/// targets exactly that person across all their devices.
/// </summary>
[Authorize]
public class ChatHub : Hub<IChatClient>
{
    /// <summary>The route the hub is mapped to (shared by the pipeline mapping and the JWT query-token rule).</summary>
    public const string Path = "/hubs/chat";
}
