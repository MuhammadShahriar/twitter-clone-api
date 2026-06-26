using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// HTTP-level integration tests for Module 10A (quote tweet). Drives the real API end-to-end
/// (<see cref="TestWebAppFactory"/>, EF Core in-memory provider): posting a tweet with <c>quotedTweetId</c>
/// embeds a one-level preview that rides every read; the quoted author gets a <c>Quote</c> notification
/// (not on self-quote); deleting the quoted tweet leaves the quote intact with a null preview; a quote needs
/// content; a missing quoted id is a 404. Fresh handles per test (shared in-memory DB).
/// </summary>
public class QuoteTweetApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public QuoteTweetApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Quoting_a_tweet_embeds_a_one_level_preview_and_notifies_the_quoted_author()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_qt1", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob_qt1", "Bob");

        var original = await CreateTweetAsync(client, alice.AccessToken, "Alice's original take.");
        var quote = await CreateQuoteAsync(client, bob.AccessToken, "Bob agrees:", original.Id);

        // The quote carries a one-level preview of the original (id, content, author).
        Assert.NotNull(quote.QuotedTweet);
        Assert.Equal(original.Id, quote.QuotedTweet!.Id);
        Assert.Equal("Alice's original take.", quote.QuotedTweet.Content);
        Assert.Equal(alice.Handle, quote.QuotedTweet.Author.Handle);

        // The preview rides the public read paths too — the feed and tweet detail.
        var fromDetail = await GetTweetAsync(client, quote.Id);
        Assert.Equal(original.Id, fromDetail.QuotedTweet!.Id);

        var feed = await GetFeedAsync(client);
        var quoteInFeed = feed.Items.Single(t => t.Id == quote.Id);
        Assert.Equal(original.Id, quoteInFeed.QuotedTweet!.Id);

        // The original now reports one quote.
        Assert.Equal(1, (await GetTweetAsync(client, original.Id)).QuoteCount);

        // Alice (the quoted author) got a real-time-eligible Quote notification from Bob, pointing at the quote.
        var aliceNotifs = await ListNotificationsAsync(client, alice.AccessToken);
        var quoteNotif = Assert.Single(aliceNotifs.Items, n => n.Type == "Quote");
        Assert.Equal(bob.Handle, quoteNotif.Actor.Handle);
        Assert.Equal(quote.Id, quoteNotif.TweetId);
    }

    [Fact]
    public async Task Self_quote_creates_no_notification()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_qt2", "Alice");

        var original = await CreateTweetAsync(client, alice.AccessToken, "Quoting myself.");
        var quote = await CreateQuoteAsync(client, alice.AccessToken, "As I was saying:", original.Id);

        Assert.Equal(original.Id, quote.QuotedTweet!.Id);

        // No self-notification.
        var notifs = await ListNotificationsAsync(client, alice.AccessToken);
        Assert.DoesNotContain(notifs.Items, n => n.Type == "Quote");
    }

    [Fact]
    public async Task Deleting_the_quoted_tweet_leaves_the_quote_with_a_null_preview()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_qt3", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob_qt3", "Bob");

        var original = await CreateTweetAsync(client, alice.AccessToken, "Soon to be deleted.");
        var quote = await CreateQuoteAsync(client, bob.AccessToken, "Quoting before it's gone.", original.Id);

        // Alice deletes her original.
        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/tweets/{original.Id}");
        delete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);

        // The quote survives; its preview is now null ("unavailable").
        var stillThere = await GetTweetAsync(client, quote.Id);
        Assert.Equal("Quoting before it's gone.", stillThere.Content);
        Assert.Null(stillThere.QuotedTweet);
    }

    [Fact]
    public async Task A_quote_must_have_content()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_qt4", "Alice");

        var original = await CreateTweetAsync(client, alice.AccessToken, "Original.");

        // A quote with empty content is a 400 (it's a comment on the quoted tweet, so text is required).
        using var form = new MultipartFormDataContent
        {
            { new StringContent(string.Empty), "content" },
            { new StringContent(original.Id.ToString()), "quotedTweetId" },
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tweets") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Quoting_a_missing_tweet_is_404()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_qt5", "Alice");

        using var form = new MultipartFormDataContent
        {
            { new StringContent("Quoting a ghost."), "content" },
            { new StringContent(Guid.NewGuid().ToString()), "quotedTweetId" },
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tweets") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(request)).StatusCode);
    }

    private static async Task<TweetResponse> GetTweetAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/tweets/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tweet = await response.Content.ReadFromJsonAsync<TweetResponse>();
        Assert.NotNull(tweet);
        return tweet!;
    }

    private static async Task<PagedTweets> GetFeedAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/tweets?limit=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedTweets>();
        Assert.NotNull(page);
        return page!;
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

    private static async Task<TweetResponse> CreateQuoteAsync(
        HttpClient client, string accessToken, string content, Guid quotedTweetId)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(content), "content" },
            { new StringContent(quotedTweetId.ToString()), "quotedTweetId" },
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

    private record PagedTweets(List<TweetResponse> Items, string? NextCursor);

    private record TweetResponse(
        Guid Id,
        string Content,
        Guid AuthorId,
        Guid? ParentId,
        int QuoteCount,
        QuotedTweetBody? QuotedTweet);

    private record QuotedTweetBody(Guid Id, string Content, QuotedAuthorBody Author, DateTime CreatedAtUtc);

    private record QuotedAuthorBody(string Handle, string DisplayName, string? AvatarUrl);

    private record PagedNotifications(List<NotificationItem> Items, string? NextCursor);

    private record NotificationItem(Guid Id, ActorBody Actor, string Type, Guid? TweetId);

    private record ActorBody(string Handle, string DisplayName, string? AvatarUrl);
}
