using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// HTTP-level integration tests for Module 12A (direct messages). Drives the real API end-to-end
/// (<see cref="TestWebAppFactory"/>, EF Core in-memory provider): get-or-create a conversation, send/list
/// messages, the conversation list with unread + preview, mark-read, the badge, participant isolation, and
/// the self/unknown/empty guards. Fresh handles per test (shared in-memory DB).
/// </summary>
public class DirectMessagesApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public DirectMessagesApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Full_dm_flow_start_send_list_read_reply_with_isolation()
    {
        var client = _factory.CreateClient();
        var a = await RegisterAndLoginAsync(client, "@dm_a1", "Alice");
        var b = await RegisterAndLoginAsync(client, "@dm_b1", "Bob");
        var c = await RegisterAndLoginAsync(client, "@dm_c1", "Carol");

        // A starts a conversation with B and sends a message.
        var conv = await StartAsync(client, a.AccessToken, handle: b.Handle);
        Assert.Equal(b.Handle, conv.OtherParticipant.Handle);
        await SendAsync(client, a.AccessToken, conv.Id, "hi Bob");

        // B's conversation list shows it: unread 1, preview + the other participant is A.
        var bList = await ListAsync(client, b.AccessToken);
        var bConv = bList.Items.Single(x => x.Id == conv.Id);
        Assert.Equal(a.Handle, bConv.OtherParticipant.Handle);
        Assert.Equal(1, bConv.UnreadCount);
        Assert.Equal("hi Bob", bConv.LastMessage!.ContentPreview);
        Assert.Equal(1, (await UnreadCountAsync(client, b.AccessToken)).UnreadCount);

        // B reads the thread (newest-first) then marks read → unread 0.
        var bMsgs = await MessagesAsync(client, b.AccessToken, conv.Id);
        Assert.Equal("hi Bob", bMsgs.Items.Single().Content);
        Assert.False(bMsgs.Items.Single().IsMine); // A sent it
        Assert.Equal(0, (await MarkReadAsync(client, b.AccessToken, conv.Id)).UnreadCount);
        Assert.Equal(0, (await UnreadCountAsync(client, b.AccessToken)).UnreadCount);
        Assert.Equal(0, ListConv(await ListAsync(client, b.AccessToken), conv.Id).UnreadCount);

        // B replies → A sees it as unread.
        await SendAsync(client, b.AccessToken, conv.Id, "hey Alice");
        Assert.Equal(1, ListConv(await ListAsync(client, a.AccessToken), conv.Id).UnreadCount);
        var aMsgs = await MessagesAsync(client, a.AccessToken, conv.Id);
        Assert.Equal(new[] { "hey Alice", "hi Bob" }, aMsgs.Items.Select(m => m.Content).ToArray()); // newest-first

        // Participant isolation: C can neither read nor post nor mark-read this conversation (403).
        Assert.Equal(HttpStatusCode.Forbidden, (await RawMessagesAsync(client, c.AccessToken, conv.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RawSendAsync(client, c.AccessToken, conv.Id, "intrude")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RawMarkReadAsync(client, c.AccessToken, conv.Id)).StatusCode);
        // ...and C's own conversation list doesn't include it.
        Assert.DoesNotContain((await ListAsync(client, c.AccessToken)).Items, x => x.Id == conv.Id);
    }

    [Fact]
    public async Task Get_or_create_is_idempotent_regardless_of_who_starts()
    {
        var client = _factory.CreateClient();
        var a = await RegisterAndLoginAsync(client, "@dm_a2", "Alice");
        var b = await RegisterAndLoginAsync(client, "@dm_b2", "Bob");

        var first = await StartAsync(client, a.AccessToken, handle: b.Handle);
        var againSame = await StartAsync(client, a.AccessToken, handle: b.Handle);
        var fromOtherSide = await StartAsync(client, b.AccessToken, handle: a.Handle);

        // One conversation per pair, whoever starts it and however many times.
        Assert.Equal(first.Id, againSame.Id);
        Assert.Equal(first.Id, fromOtherSide.Id);
    }

    [Fact]
    public async Task Cannot_message_yourself_400_and_unknown_recipient_404()
    {
        var client = _factory.CreateClient();
        var a = await RegisterAndLoginAsync(client, "@dm_a3", "Alice");

        Assert.Equal(HttpStatusCode.BadRequest, (await RawStartAsync(client, a.AccessToken, handle: a.Handle)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RawStartAsync(client, a.AccessToken, handle: "@nobody_dm")).StatusCode);
    }

    [Fact]
    public async Task Send_requires_non_empty_content_and_bumps_recency()
    {
        var client = _factory.CreateClient();
        var a = await RegisterAndLoginAsync(client, "@dm_a4", "Alice");
        var b = await RegisterAndLoginAsync(client, "@dm_b4", "Bob");
        var c2 = await RegisterAndLoginAsync(client, "@dm_c4", "Carol");

        var convB = await StartAsync(client, a.AccessToken, handle: b.Handle);
        var convC = await StartAsync(client, a.AccessToken, handle: c2.Handle);

        // Empty content → 400.
        Assert.Equal(HttpStatusCode.BadRequest, (await RawSendAsync(client, a.AccessToken, convB.Id, "  ")).StatusCode);

        // Message convB second so it sorts above convC in A's list (recency bump).
        await SendAsync(client, a.AccessToken, convC.Id, "to carol");
        await SendAsync(client, a.AccessToken, convB.Id, "to bob");
        var list = await ListAsync(client, a.AccessToken);
        Assert.Equal(convB.Id, list.Items.First().Id);
    }

    [Fact]
    public async Task All_endpoints_require_auth()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/conversations")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/conversations/unread-count")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync("/api/conversations", JsonContent.Create(new { recipientHandle = "@x" }))).StatusCode);
    }

    // ---- helpers ----

    private static ConversationItem ListConv(PagedConversations page, Guid id) => page.Items.Single(x => x.Id == id);

    private static async Task<ConversationItem> StartAsync(HttpClient client, string token, string handle)
    {
        var resp = await RawStartAsync(client, token, handle);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<ConversationItem>())!;
    }

    private static Task<HttpResponseMessage> RawStartAsync(HttpClient client, string token, string handle) =>
        Send(client, HttpMethod.Post, "/api/conversations", token, new { recipientHandle = handle });

    private static async Task<MessageItem> SendAsync(HttpClient client, string token, Guid convId, string content)
    {
        var resp = await RawSendAsync(client, token, convId, content);
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<MessageItem>())!;
    }

    private static Task<HttpResponseMessage> RawSendAsync(HttpClient client, string token, Guid convId, string content) =>
        Send(client, HttpMethod.Post, $"/api/conversations/{convId}/messages", token, new { content });

    private static async Task<PagedConversations> ListAsync(HttpClient client, string token)
    {
        var resp = await Send(client, HttpMethod.Get, "/api/conversations?limit=50", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<PagedConversations>())!;
    }

    private static async Task<PagedMessages> MessagesAsync(HttpClient client, string token, Guid convId)
    {
        var resp = await RawMessagesAsync(client, token, convId);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<PagedMessages>())!;
    }

    private static Task<HttpResponseMessage> RawMessagesAsync(HttpClient client, string token, Guid convId) =>
        Send(client, HttpMethod.Get, $"/api/conversations/{convId}/messages?limit=50", token);

    private static async Task<UnreadBody> MarkReadAsync(HttpClient client, string token, Guid convId)
    {
        var resp = await RawMarkReadAsync(client, token, convId);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<UnreadBody>())!;
    }

    private static Task<HttpResponseMessage> RawMarkReadAsync(HttpClient client, string token, Guid convId) =>
        Send(client, HttpMethod.Post, $"/api/conversations/{convId}/read", token);

    private static async Task<UnreadBody> UnreadCountAsync(HttpClient client, string token)
    {
        var resp = await Send(client, HttpMethod.Get, "/api/conversations/unread-count", token);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<UnreadBody>())!;
    }

    private static Task<HttpResponseMessage> Send(
        HttpClient client, HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request);
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

    private record PagedConversations(List<ConversationItem> Items, string? NextCursor);

    private record ConversationItem(
        Guid Id, ChatUser OtherParticipant, LastMessage? LastMessage, int UnreadCount, DateTime LastMessageAtUtc);

    private record LastMessage(string ContentPreview, DateTime CreatedAtUtc, Guid SenderId);

    private record PagedMessages(List<MessageItem> Items, string? NextCursor);

    private record MessageItem(
        Guid Id, Guid ConversationId, Guid SenderId, ChatUser Sender, string Content, DateTime CreatedAtUtc, bool IsMine);

    private record ChatUser(Guid Id, string Handle, string DisplayName, string? AvatarUrl);

    private record UnreadBody(int UnreadCount);
}
