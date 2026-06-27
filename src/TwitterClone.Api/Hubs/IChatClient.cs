using TwitterClone.Application.Conversations;

namespace TwitterClone.Api.Hubs;

/// <summary>
/// The strongly-typed set of methods the server can invoke on a connected chat client. A client subscribes
/// to <c>ReceiveMessage</c> to get each new direct message (and its updated DM unread count) pushed the
/// moment it is committed. This is the wire contract for the frontend SignalR client wiring.
/// </summary>
public interface IChatClient
{
    Task ReceiveMessage(MessagePushDto payload);
}
