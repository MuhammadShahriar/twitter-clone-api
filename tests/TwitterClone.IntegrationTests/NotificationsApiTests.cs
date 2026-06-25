using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// HTTP-level integration tests for Module 5A (notifications). Drives the real API end-to-end
/// (<see cref="TestWebAppFactory"/>, EF Core in-memory provider): the four social actions create a
/// notification for the recipient (never the actor), with unread-dedup; and the recipient can list, count
/// unread, and mark all read. Fresh handles per test (shared in-memory DB), membership asserted by id/type.
/// </summary>
public class NotificationsApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public NotificationsApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Four_actions_notify_the_recipient_with_actor_and_preview()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_n1", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob_n1", "Bob");

        var bobTweet = await CreateTweetAsync(client, bob.AccessToken, "Bob's tweet.");

        // Alice acts on Bob in all four ways.
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/like", alice.AccessToken);
        await SendUserActionAsync(client, HttpMethod.Post, $"/api/users/{bob.Handle}/follow", alice.AccessToken);
        var reply = await CreateReplyAsync(client, alice.AccessToken, "Alice's reply.", bobTweet.Id);
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/retweet", alice.AccessToken);

        // Bob has four notifications, newest-first, all from Alice.
        var page = await ListNotificationsAsync(client, bob.AccessToken);
        Assert.Equal(new[] { "Retweet", "Reply", "Follow", "Like" }, page.Items.Select(n => n.Type).ToArray());
        Assert.All(page.Items, n => Assert.Equal(alice.Handle, n.Actor.Handle));
        Assert.All(page.Items, n => Assert.False(n.IsRead));

        // Like/retweet point at Bob's tweet and preview it; the reply points at the reply and previews it;
        // the follow has no tweet/preview.
        var byType = page.Items.ToDictionary(n => n.Type);
        Assert.Equal(bobTweet.Id, byType["Like"].TweetId);
        Assert.Equal("Bob's tweet.", byType["Like"].TweetPreview);
        Assert.Equal(bobTweet.Id, byType["Retweet"].TweetId);
        Assert.Equal(reply.Id, byType["Reply"].TweetId);
        Assert.Equal("Alice's reply.", byType["Reply"].TweetPreview);
        Assert.Null(byType["Follow"].TweetId);
        Assert.Null(byType["Follow"].TweetPreview);

        // Unread count agrees.
        Assert.Equal(4, (await GetUnreadCountAsync(client, bob.AccessToken)).UnreadCount);
    }

    [Fact]
    public async Task You_are_never_notified_of_your_own_actions()
    {
        var client = _factory.CreateClient();
        var bob = await RegisterAndLoginAsync(client, "@bob_self", "Bob");

        var bobTweet = await CreateTweetAsync(client, bob.AccessToken, "Bob talks to himself.");

        // Bob likes, retweets, and self-replies to his own tweet.
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/like", bob.AccessToken);
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/retweet", bob.AccessToken);
        await CreateReplyAsync(client, bob.AccessToken, "Talking to myself.", bobTweet.Id);

        // No self-notifications were created.
        var page = await ListNotificationsAsync(client, bob.AccessToken);
        Assert.Empty(page.Items);
        Assert.Equal(0, (await GetUnreadCountAsync(client, bob.AccessToken)).UnreadCount);
    }

    [Fact]
    public async Task Repeated_like_does_not_stack_an_unread_notification()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_dup", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob_dup", "Bob");

        var bobTweet = await CreateTweetAsync(client, bob.AccessToken, "Bob's tweet.");

        // like -> unlike -> like again. The second like is a genuine new action, but an equivalent UNREAD
        // notification already exists, so it must not create a second one.
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/like", alice.AccessToken);
        await SendTweetActionAsync(client, HttpMethod.Delete, $"/api/tweets/{bobTweet.Id}/like", alice.AccessToken);
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/like", alice.AccessToken);

        var page = await ListNotificationsAsync(client, bob.AccessToken);
        Assert.Single(page.Items, n => n.Type == "Like");
    }

    [Fact]
    public async Task Mark_all_read_zeroes_the_count_and_a_later_action_creates_a_fresh_unread()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_mr", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob_mr", "Bob");

        var bobTweet = await CreateTweetAsync(client, bob.AccessToken, "Bob's tweet.");
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/like", alice.AccessToken);
        Assert.Equal(1, (await GetUnreadCountAsync(client, bob.AccessToken)).UnreadCount);

        // Mark all read -> count 0, and the items are still listed but flagged read.
        var afterRead = await MarkAllReadAsync(client, bob.AccessToken);
        Assert.Equal(0, afterRead.UnreadCount);
        Assert.Equal(0, (await GetUnreadCountAsync(client, bob.AccessToken)).UnreadCount);
        var listed = await ListNotificationsAsync(client, bob.AccessToken);
        Assert.All(listed.Items, n => Assert.True(n.IsRead));

        // Now that the earlier like is READ, a genuine new like creates a fresh UNREAD notification.
        await SendTweetActionAsync(client, HttpMethod.Delete, $"/api/tweets/{bobTweet.Id}/like", alice.AccessToken);
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/like", alice.AccessToken);
        Assert.Equal(1, (await GetUnreadCountAsync(client, bob.AccessToken)).UnreadCount);
    }

    [Fact]
    public async Task Listing_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notifications/unread-count")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/notifications/read", null)).StatusCode);
    }

    private static async Task<PagedNotifications> ListNotificationsAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/notifications?limit=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedNotifications>();
        Assert.NotNull(page);
        return page!;
    }

    private static async Task<UnreadCountBody> GetUnreadCountAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/notifications/unread-count");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UnreadCountBody>();
        Assert.NotNull(body);
        return body!;
    }

    private static async Task<UnreadCountBody> MarkAllReadAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/notifications/read");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UnreadCountBody>();
        Assert.NotNull(body);
        return body!;
    }

    private static async Task<TweetResponse> SendTweetActionAsync(
        HttpClient client, HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tweet = await response.Content.ReadFromJsonAsync<TweetResponse>();
        Assert.NotNull(tweet);
        return tweet!;
    }

    private static async Task SendUserActionAsync(HttpClient client, HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<TweetResponse> CreateTweetAsync(HttpClient client, string accessToken, string content)
    {
        using var form = new MultipartFormDataContent { { new StringContent(content), "content" } };
        return await PostTweetFormAsync(client, accessToken, form);
    }

    private static async Task<TweetResponse> CreateReplyAsync(
        HttpClient client, string accessToken, string content, Guid parentId)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(content), "content" },
            { new StringContent(parentId.ToString()), "parentId" },
        };
        return await PostTweetFormAsync(client, accessToken, form);
    }

    private static async Task<TweetResponse> PostTweetFormAsync(
        HttpClient client, string accessToken, MultipartFormDataContent form)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tweets") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TweetResponse>();
        Assert.NotNull(created);
        return created!;
    }

    private static async Task<AuthedUser> RegisterAndLoginAsync(HttpClient client, string handle, string displayName)
    {
        var email = $"{handle.TrimStart('@')}@example.com";
        const string password = "P@ssw0rd!";

        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, handle, displayName, password });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<LoginBody>();
        Assert.NotNull(auth);
        return new AuthedUser(auth!.AccessToken, auth.UserId, auth.Handle);
    }

    private record AuthedUser(string AccessToken, Guid UserId, string Handle);

    private record LoginBody(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Handle, string DisplayName);

    private record PagedNotifications(List<NotificationItem> Items, string? NextCursor);

    private record NotificationItem(
        Guid Id,
        ActorBody Actor,
        string Type,
        bool IsRead,
        DateTime CreatedAtUtc,
        Guid? TweetId,
        string? TweetPreview);

    private record ActorBody(string Handle, string DisplayName, string? AvatarUrl);

    private record UnreadCountBody(int UnreadCount);

    private record TweetResponse(Guid Id, string Content, Guid AuthorId, Guid? ParentId);
}
