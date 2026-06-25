using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// HTTP-level integration tests for Module 6A (bookmarks). Drives the real API end-to-end
/// (<see cref="TestWebAppFactory"/>, EF Core in-memory provider): bookmark/un-bookmark is idempotent and
/// auth'd; <c>bookmarkedByCurrentUser</c> surfaces on tweet reads; <c>GET /api/bookmarks</c> is private to the
/// caller. Fresh handles per test (shared in-memory DB).
/// </summary>
public class BookmarksApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public BookmarksApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Bookmark_flags_the_tweet_lists_it_then_unbookmark_removes_it()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_bm", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob_bm", "Bob");

        var bobTweet = await CreateTweetAsync(client, bob.AccessToken, "Bob's bookmarkable tweet.");

        // Bookmark -> response carries the flag, and the tweet appears in Alice's bookmarks.
        var afterBookmark = await SendTweetActionAsync(
            client, HttpMethod.Post, $"/api/tweets/{bobTweet.Id}/bookmark", alice.AccessToken);
        Assert.True(afterBookmark.BookmarkedByCurrentUser);

        var bookmarks = await ListBookmarksAsync(client, alice.AccessToken);
        Assert.Contains(bookmarks.Items, t => t.Id == bobTweet.Id);
        Assert.All(bookmarks.Items, t => Assert.True(t.BookmarkedByCurrentUser));

        // The flag also shows on a normal authenticated feed read.
        var feed = await ReadFeedAsync(client, alice.AccessToken);
        Assert.True(feed.Items.Single(t => t.Id == bobTweet.Id).BookmarkedByCurrentUser);

        // Un-bookmark -> flag clears and it drops out of the bookmarks list.
        var afterUnbookmark = await SendTweetActionAsync(
            client, HttpMethod.Delete, $"/api/tweets/{bobTweet.Id}/bookmark", alice.AccessToken);
        Assert.False(afterUnbookmark.BookmarkedByCurrentUser);

        var afterList = await ListBookmarksAsync(client, alice.AccessToken);
        Assert.DoesNotContain(afterList.Items, t => t.Id == bobTweet.Id);
    }

    [Fact]
    public async Task Bookmarking_and_unbookmarking_are_idempotent()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_bm_idem", "Alice");

        var tweet = await CreateTweetAsync(client, alice.AccessToken, "Idempotency tweet.");

        // Double bookmark stays 200 and ends bookmarked (just one row).
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{tweet.Id}/bookmark", alice.AccessToken);
        var second = await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{tweet.Id}/bookmark", alice.AccessToken);
        Assert.True(second.BookmarkedByCurrentUser);
        Assert.Single((await ListBookmarksAsync(client, alice.AccessToken)).Items, t => t.Id == tweet.Id);

        // Double un-bookmark stays 200 and ends not bookmarked.
        await SendTweetActionAsync(client, HttpMethod.Delete, $"/api/tweets/{tweet.Id}/bookmark", alice.AccessToken);
        var secondRemove = await SendTweetActionAsync(client, HttpMethod.Delete, $"/api/tweets/{tweet.Id}/bookmark", alice.AccessToken);
        Assert.False(secondRemove.BookmarkedByCurrentUser);
    }

    [Fact]
    public async Task Bookmarks_are_private_to_each_user()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_bm_priv", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob_bm_priv", "Bob");

        var tweet = await CreateTweetAsync(client, bob.AccessToken, "A tweet to save.");
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{tweet.Id}/bookmark", alice.AccessToken);

        // Bob does not see Alice's bookmark — not in his list, and his by-me flag on that tweet is false.
        var bobBookmarks = await ListBookmarksAsync(client, bob.AccessToken);
        Assert.DoesNotContain(bobBookmarks.Items, t => t.Id == tweet.Id);

        var bobFeed = await ReadFeedAsync(client, bob.AccessToken);
        Assert.False(bobFeed.Items.Single(t => t.Id == tweet.Id).BookmarkedByCurrentUser);
    }

    [Fact]
    public async Task Bookmarking_a_missing_tweet_is_404()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_bm_404", "Alice");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tweets/{Guid.NewGuid()}/bookmark");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", alice.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Bookmark_endpoints_require_a_token()
    {
        var client = _factory.CreateClient();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync($"/api/tweets/{id}/bookmark", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync($"/api/tweets/{id}/bookmark")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/bookmarks")).StatusCode);
    }

    private static async Task<PagedTweets> ListBookmarksAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/bookmarks?limit=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedTweets>();
        Assert.NotNull(page);
        return page!;
    }

    private static async Task<PagedTweets> ReadFeedAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/tweets?limit=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedTweets>();
        Assert.NotNull(page);
        return page!;
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
        return new AuthedUser(auth!.AccessToken, auth.Handle);
    }

    private record AuthedUser(string AccessToken, string Handle);

    private record LoginBody(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Handle, string DisplayName);

    private record PagedTweets(List<TweetResponse> Items, string? NextCursor);

    private record TweetResponse(Guid Id, string Content, bool BookmarkedByCurrentUser);
}
