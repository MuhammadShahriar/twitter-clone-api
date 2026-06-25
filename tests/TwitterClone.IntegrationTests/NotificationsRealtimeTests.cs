using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Notifications;
using TwitterClone.Domain.Enums;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// HTTP-level tests for Module 5B (real-time push). Rather than open a live WebSocket, these verify the
/// server-side wiring at the seam that matters: the commit chokepoint (UnitOfWork) builds the push payload
/// and calls <see cref="INotificationPublisher"/> exactly when — and only when — a notification row is
/// committed. A recording fake publisher stands in for SignalR, so we can assert what would be pushed
/// (recipient, payload, unread count) after each social action, plus self-skip / dedup suppression. A
/// separate test checks the hub itself requires a JWT (and accepts it via the query string).
/// </summary>
public class NotificationsRealtimeTests : IClassFixture<RecordingPublisherWebAppFactory>
{
    private readonly RecordingPublisherWebAppFactory _factory;

    public NotificationsRealtimeTests(RecordingPublisherWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Like_pushes_the_notification_and_unread_count_to_the_recipient()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_rt1", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob_rt1", "Bob");

        var bobTweet = await CreateTweetAsync(client, bob.AccessToken, "Bob's tweet.");
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/like", alice.AccessToken);

        var push = Assert.Single(_factory.Publisher.PushesTo(bob.UserId));
        Assert.Equal(1, push.Payload.UnreadCount);
        Assert.Equal(NotificationType.Like, push.Payload.Notification.Type);
        Assert.Equal(alice.Handle, push.Payload.Notification.Actor.Handle);
        Assert.Equal(bobTweet.Id, push.Payload.Notification.TweetId);
        Assert.Equal("Bob's tweet.", push.Payload.Notification.TweetPreview);
        Assert.False(push.Payload.Notification.IsRead);

        // The actor (Alice) is never pushed to.
        Assert.Empty(_factory.Publisher.PushesTo(alice.UserId));
    }

    [Fact]
    public async Task A_self_action_pushes_nothing()
    {
        var client = _factory.CreateClient();
        var bob = await RegisterAndLoginAsync(client, "@bob_rt2", "Bob");

        var bobTweet = await CreateTweetAsync(client, bob.AccessToken, "Bob's tweet.");
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/like", bob.AccessToken);
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/retweet", bob.AccessToken);

        Assert.Empty(_factory.Publisher.PushesTo(bob.UserId));
    }

    [Fact]
    public async Task A_deduped_repeat_like_does_not_push_again()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_rt3", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob_rt3", "Bob");

        var bobTweet = await CreateTweetAsync(client, bob.AccessToken, "Bob's tweet.");

        // like -> unlike -> like: the unlike creates no notification, and the second like is de-duplicated
        // against the still-unread one, so only the FIRST like results in a push.
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/like", alice.AccessToken);
        await SendTweetActionAsync(client, HttpMethod.Delete, $"/api/tweets/{bobTweet.Id}/like", alice.AccessToken);
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/like", alice.AccessToken);

        Assert.Single(_factory.Publisher.PushesTo(bob.UserId));
    }

    [Fact]
    public async Task The_hub_requires_a_jwt_and_accepts_it_in_the_query_string()
    {
        var client = _factory.CreateClient();

        // No token -> the negotiate handshake is rejected by [Authorize].
        var anonymous = await client.PostAsync("/hubs/notifications/negotiate", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        // A valid token in the access_token query string is accepted (negotiate succeeds).
        var user = await RegisterAndLoginAsync(client, "@socket_user", "Socket");
        var authed = await client.PostAsync(
            $"/hubs/notifications/negotiate?access_token={user.AccessToken}", content: null);
        Assert.Equal(HttpStatusCode.OK, authed.StatusCode);
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

    private static async Task<TweetResponse> CreateTweetAsync(HttpClient client, string accessToken, string content)
    {
        using var form = new MultipartFormDataContent { { new StringContent(content), "content" } };
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

    private record TweetResponse(Guid Id, string Content, Guid AuthorId, Guid? ParentId);
}

/// <summary>
/// A <see cref="TestWebAppFactory"/> that swaps the SignalR-backed <see cref="INotificationPublisher"/> for a
/// recording fake, so tests can assert what the commit chokepoint would push without a live WebSocket.
/// </summary>
public class RecordingPublisherWebAppFactory : TestWebAppFactory
{
    public RecordingNotificationPublisher Publisher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            var existing = services.FirstOrDefault(d => d.ServiceType == typeof(INotificationPublisher));
            if (existing is not null)
            {
                services.Remove(existing);
            }

            services.AddSingleton<INotificationPublisher>(Publisher);
        });
    }
}

/// <summary>Records every push the commit chokepoint makes, for assertion in tests.</summary>
public sealed class RecordingNotificationPublisher : INotificationPublisher
{
    private readonly ConcurrentQueue<(Guid RecipientId, NotificationPushDto Payload)> _pushes = new();

    public Task PublishAsync(Guid recipientId, NotificationPushDto payload, CancellationToken ct = default)
    {
        _pushes.Enqueue((recipientId, payload));
        return Task.CompletedTask;
    }

    public IReadOnlyList<(Guid RecipientId, NotificationPushDto Payload)> PushesTo(Guid recipientId) =>
        _pushes.Where(p => p.RecipientId == recipientId).ToArray();
}
