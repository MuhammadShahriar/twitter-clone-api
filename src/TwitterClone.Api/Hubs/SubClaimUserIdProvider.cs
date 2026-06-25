using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.SignalR;

namespace TwitterClone.Api.Hubs;

/// <summary>
/// Maps a SignalR connection to its user id using the JWT <c>sub</c> claim (the user's Guid). The default
/// provider reads <c>ClaimTypes.NameIdentifier</c>, but JWT bearer is configured with
/// <c>MapInboundClaims = false</c>, so the claim keeps its original name <c>sub</c> — matching what
/// <c>CurrentUserService</c> reads and what the publisher targets via <c>Clients.User(recipientId)</c>.
/// </summary>
public class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
