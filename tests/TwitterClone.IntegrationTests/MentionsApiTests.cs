using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// HTTP-level integration tests for Module 9A (mentions). Posting a tweet/reply containing <c>@handle</c>
/// creates a <c>Mention</c> notification for each mentioned user, reusing the Module 5 notification system.
/// Drives the real API end-to-end (<see cref="TestWebAppFactory"/>, EF Core in-memory provider). Asserts the
/// self-mention/unknown-handle/duplicate rules and the reply-vs-mention de-dupe. Fresh handles per test.
/// </summary>
public class MentionsApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public MentionsApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Mentioning_users_notifies_each_once_ignoring_self_and_unknown()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@author_m1", "Author");
        var alice = await RegisterAndLoginAsync(client, "@alice_m1", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob_m1", "Bob");

        // The author mentions Alice twice, Bob once, themselves, and an unknown handle.
        await CreateTweetAsync(
            client, author.AccessToken, "hey @alice_m1 @bob_m1 @alice_m1 and @author_m1 and @nobody_m1");

        // Alice: exactly one Mention from the author, previewing the tweet.
        var aliceNotifs = await ListNotificationsAsync(client, alice.AccessToken);
        var aliceMention = Assert.Single(aliceNotifs.Items, n => n.Type == "Mention");
        Assert.Equal(author.Handle, aliceMention.Actor.Handle);
        Assert.Contains("@alice_m1", aliceMention.TweetPreview);

        // Bob: exactly one Mention.
        var bobNotifs = await ListNotificationsAsync(client, bob.AccessToken);
        Assert.Single(bobNotifs.Items, n => n.Type == "Mention");

        // The author never gets a self-mention; "@nobody_m1" resolves to no one, so nothing leaks.
        var authorNotifs = await ListNotificationsAsync(client, author.AccessToken);
        Assert.Empty(authorNotifs.Items);
    }

    [Fact]
    public async Task Mentioning_the_same_handle_twice_in_one_tweet_creates_a_single_mention()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@author_m2", "Author");
        var alice = await RegisterAndLoginAsync(client, "@alice_m2", "Alice");

        // The same handle repeated within a single tweet must not stack (parser de-dups; the service's
        // per-(recipient, actor, type, tweet) unread guard is the backstop).
        await CreateTweetAsync(client, author.AccessToken, "@alice_m2 hey @alice_m2 again @ALICE_M2");

        var notifs = await ListNotificationsAsync(client, alice.AccessToken);
        Assert.Single(notifs.Items, n => n.Type == "Mention");
    }

    [Fact]
    public async Task Two_tweets_each_mentioning_a_user_produce_two_mentions()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@author_m5", "Author");
        var alice = await RegisterAndLoginAsync(client, "@alice_m5", "Alice");

        // De-dup is per-tweet, not global: two distinct tweets mentioning Alice are two genuine mentions
        // (each about a different tweet), so both surface.
        await CreateTweetAsync(client, author.AccessToken, "first @alice_m5");
        await CreateTweetAsync(client, author.AccessToken, "second @alice_m5");

        var notifs = await ListNotificationsAsync(client, alice.AccessToken);
        Assert.Equal(2, notifs.Items.Count(n => n.Type == "Mention"));
    }

    [Fact]
    public async Task A_reply_that_mentions_the_parent_author_yields_only_a_reply_not_a_mention()
    {
        var client = _factory.CreateClient();
        var bob = await RegisterAndLoginAsync(client, "@bob_m3", "Bob");
        var alice = await RegisterAndLoginAsync(client, "@alice_m3", "Alice");

        var bobTweet = await CreateTweetAsync(client, bob.AccessToken, "Bob's tweet.");

        // Alice replies to Bob and also @-mentions him in the reply. Bob should get a single Reply, no Mention.
        await CreateReplyAsync(client, alice.AccessToken, "thanks @bob_m3 for this", bobTweet.Id);

        var bobNotifs = await ListNotificationsAsync(client, bob.AccessToken);
        Assert.Single(bobNotifs.Items, n => n.Type == "Reply");
        Assert.DoesNotContain(bobNotifs.Items, n => n.Type == "Mention");
    }

    [Fact]
    public async Task A_reply_that_mentions_a_third_party_notifies_them_with_a_mention()
    {
        var client = _factory.CreateClient();
        var bob = await RegisterAndLoginAsync(client, "@bob_m4", "Bob");
        var alice = await RegisterAndLoginAsync(client, "@alice_m4", "Alice");
        var carol = await RegisterAndLoginAsync(client, "@carol_m4", "Carol");

        var bobTweet = await CreateTweetAsync(client, bob.AccessToken, "Bob's tweet.");

        // Alice replies to Bob but mentions Carol (a third party). Bob gets the Reply; Carol gets a Mention.
        await CreateReplyAsync(client, alice.AccessToken, "cc @carol_m4", bobTweet.Id);

        Assert.Single((await ListNotificationsAsync(client, bob.AccessToken)).Items, n => n.Type == "Reply");
        Assert.Single((await ListNotificationsAsync(client, carol.AccessToken)).Items, n => n.Type == "Mention");
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

    private record TweetResponse(Guid Id, string Content, Guid AuthorId, Guid? ParentId);
}
