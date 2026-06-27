using Microsoft.AspNetCore.SignalR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Conversations;

namespace TwitterClone.Api.Hubs;

/// <summary>
/// SignalR implementation of <see cref="IMessagePublisher"/>: delivers a message to all of the recipient's
/// live connections via the typed hub. Lives in the API layer (SignalR is an ASP.NET Core concern) so the
/// Application/Infrastructure layers stay transport-agnostic — exactly like <see cref="NotificationHubPublisher"/>.
/// Targeting by user id means every device the recipient has open receives the push; if they have none, it
/// is a harmless no-op.
/// </summary>
public class ChatHubPublisher(IHubContext<ChatHub, IChatClient> hub) : IMessagePublisher
{
    public Task PublishAsync(Guid recipientId, MessagePushDto payload, CancellationToken ct = default) =>
        hub.Clients.User(recipientId.ToString()).ReceiveMessage(payload);
}
