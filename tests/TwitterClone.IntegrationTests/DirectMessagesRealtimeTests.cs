using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Conversations;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// HTTP-level tests for Module 12B (real-time DM push). Like the 5B notification realtime tests, they verify
/// the server-side seam — the commit chokepoint (UnitOfWork) projects the message and calls
/// <see cref="IMessagePublisher"/> after the send commits — using a recording fake publisher instead of a
/// live WebSocket. They assert the recipient (not the sender) receives the right <c>ReceiveMessage</c>
/// payload + unread count, and that the chat hub requires a JWT (accepted via the query string).
/// </summary>
public class DirectMessagesRealtimeTests : IClassFixture<RecordingPublisherWebAppFactory>
{
    private readonly RecordingPublisherWebAppFactory _factory;

    public DirectMessagesRealtimeTests(RecordingPublisherWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Sending_a_message_pushes_ReceiveMessage_to_the_recipient_only()
    {
        var client = _factory.CreateClient();
        var a = await RegisterAndLoginAsync(client, "@dm_rt_a", "Alice");
        var b = await RegisterAndLoginAsync(client, "@dm_rt_b", "Bob");

        var conv = await StartAsync(client, a.AccessToken, b.Handle);
        await SendAsync(client, a.AccessToken, conv.Id, "hi Bob");

        // The recipient (Bob) gets exactly one push, after commit, with the projected message + his DM badge.
        var push = Assert.Single(_factory.Messages.PushesTo(b.UserId));
        Assert.Equal(conv.Id, push.Payload.ConversationId);
        Assert.Equal(1, push.Payload.UnreadCount);                 // one conversation now has unread for Bob
        Assert.Equal("hi Bob", push.Payload.Message.Content);
        Assert.Equal(conv.Id, push.Payload.Message.ConversationId);
        Assert.Equal(a.UserId, push.Payload.Message.SenderId);
        Assert.Equal(a.Handle, push.Payload.Message.Sender.Handle);
        Assert.False(push.Payload.Message.IsMine);                 // projected for the recipient, not the sender

        // The sender (Alice) is not echoed to (sender-device echo is deferred).
        Assert.Empty(_factory.Messages.PushesTo(a.UserId));
    }

    [Fact]
    public async Task The_chat_hub_requires_a_jwt_and_accepts_it_in_the_query_string()
    {
        var client = _factory.CreateClient();

        // No token -> the negotiate handshake is rejected by [Authorize].
        var anonymous = await client.PostAsync("/hubs/chat/negotiate", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // A valid token in the access_token query string is accepted (negotiate succeeds).
        var user = await RegisterAndLoginAsync(client, "@chat_socket_user", "Socket");
        var authed = await client.PostAsync($"/hubs/chat/negotiate?access_token={user.AccessToken}", content: null);
        Assert.Equal(HttpStatusCode.OK, authed.StatusCode);
    }

    private static async Task<ConversationItem> StartAsync(HttpClient client, string token, string handle)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/conversations")
        {
            Content = JsonContent.Create(new { recipientHandle = handle }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<ConversationItem>())!;
    }

    private static async Task SendAsync(HttpClient client, string token, Guid convId, string content)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/conversations/{convId}/messages")
        {
            Content = JsonContent.Create(new { content }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    private static async Task<AuthedUser> RegisterAndLoginAsync(HttpClient client, string handle, string displayName)
    {
        var email = $"{handle.TrimStart('@')}@example.com";
        const string password = "P@ssw0rd!";
        var reg = await client.PostAsJsonAsync("/api/auth/register", new { email, handle, displayName, password });
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<LoginBody>();
        return new AuthedUser(auth!.AccessToken, auth.UserId, auth.Handle);
    }

    private record AuthedUser(string AccessToken, Guid UserId, string Handle);

    private record LoginBody(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Handle, string DisplayName);

    private record ConversationItem(Guid Id);
}

/// <summary>
/// Tests that a real-time push failure never breaks the send (best-effort delivery): the send still returns
/// 201 and the message is persisted/readable. Uses a publisher that always throws.
/// </summary>
public class DirectMessagesPublishResilienceTests : IClassFixture<ThrowingMessagePublisherWebAppFactory>
{
    private readonly ThrowingMessagePublisherWebAppFactory _factory;

    public DirectMessagesPublishResilienceTests(ThrowingMessagePublisherWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task A_failing_push_does_not_break_the_send()
    {
        var client = _factory.CreateClient();
        var a = await RegisterAndLoginAsync(client, "@dm_res_a", "Alice");
        var b = await RegisterAndLoginAsync(client, "@dm_res_b", "Bob");

        var conv = await StartAsync(client, a.AccessToken, b.Handle);

        // The publisher throws, but the send already committed → 201, and the message is readable afterwards.
        var sendReq = new HttpRequestMessage(HttpMethod.Post, $"/api/conversations/{conv.Id}/messages")
        {
            Content = JsonContent.Create(new { content = "still delivered" }),
        };
        sendReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", a.AccessToken);
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(sendReq)).StatusCode);

        var listReq = new HttpRequestMessage(HttpMethod.Get, $"/api/conversations/{conv.Id}/messages");
        listReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", b.AccessToken);
        var listResp = await client.SendAsync(listReq);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var page = await listResp.Content.ReadFromJsonAsync<PagedMessages>();
        Assert.Contains(page!.Items, m => m.Content == "still delivered");
    }

    private static async Task<ConversationItem> StartAsync(HttpClient client, string token, string handle)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/conversations")
        {
            Content = JsonContent.Create(new { recipientHandle = handle }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<ConversationItem>())!;
    }

    private static async Task<AuthedUser> RegisterAndLoginAsync(HttpClient client, string handle, string displayName)
    {
        var email = $"{handle.TrimStart('@')}@example.com";
        const string password = "P@ssw0rd!";
        await client.PostAsJsonAsync("/api/auth/register", new { email, handle, displayName, password });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var auth = await login.Content.ReadFromJsonAsync<LoginBody>();
        return new AuthedUser(auth!.AccessToken, auth.UserId, auth.Handle);
    }

    private record AuthedUser(string AccessToken, Guid UserId, string Handle);

    private record LoginBody(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Handle, string DisplayName);

    private record ConversationItem(Guid Id);

    private record PagedMessages(List<MessageItem> Items, string? NextCursor);

    private record MessageItem(Guid Id, string Content);
}

/// <summary>A <see cref="TestWebAppFactory"/> whose message publisher always throws, to test send resilience.</summary>
public class ThrowingMessagePublisherWebAppFactory : TestWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            var existing = services.FirstOrDefault(d => d.ServiceType == typeof(IMessagePublisher));
            if (existing is not null)
            {
                services.Remove(existing);
            }

            services.AddSingleton<IMessagePublisher, ThrowingMessagePublisher>();
        });
    }

    private sealed class ThrowingMessagePublisher : IMessagePublisher
    {
        public Task PublishAsync(Guid recipientId, MessagePushDto payload, CancellationToken ct = default) =>
            throw new InvalidOperationException("simulated push failure");
    }
}
