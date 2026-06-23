using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Integration tests for Module 3B: the follow graph, the lite user endpoint, and the Following feed.
/// Drives the real API end-to-end (see <see cref="TestWebAppFactory"/>, EF Core in-memory provider). The
/// factory is a class fixture, so the in-memory DB is shared across the tests below — each test uses fresh
/// handles and asserts membership (by id) rather than absolute counts, so they don't collide.
/// </summary>
public class FollowAndFollowingFeedTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public FollowAndFollowingFeedTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Follow_updates_target_counts_and_flag()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice3b", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob3b", "Bob");

        // Before following: Alice does not follow Bob, who has no followers.
        var before = await GetUserAsAsync(client, bob.Handle, alice.AccessToken);
        Assert.False(before.IsFollowedByCurrentUser);
        Assert.Equal(0, before.FollowerCount);

        // Follow -> flag true, follower count 1 (response body reflects it immediately).
        var followed = await SendUserActionAsync(client, HttpMethod.Post, $"/api/users/{bob.Handle}/follow", alice.AccessToken);
        Assert.True(followed.IsFollowedByCurrentUser);
        Assert.Equal(1, followed.FollowerCount);

        // A fresh GET as Alice agrees; Alice's own followingCount is now 1.
        var bobAsAlice = await GetUserAsAsync(client, bob.Handle, alice.AccessToken);
        Assert.True(bobAsAlice.IsFollowedByCurrentUser);
        Assert.Equal(1, bobAsAlice.FollowerCount);

        var aliceAsAlice = await GetUserAsAsync(client, alice.Handle, alice.AccessToken);
        Assert.Equal(1, aliceAsAlice.FollowingCount);

        // Following again is idempotent -> still 1.
        var followedAgain = await SendUserActionAsync(client, HttpMethod.Post, $"/api/users/{bob.Handle}/follow", alice.AccessToken);
        Assert.Equal(1, followedAgain.FollowerCount);

        // The flag is per-viewer: an anonymous reader sees the count but no flag.
        var bobAnon = await client.GetFromJsonAsync<UserResponse>($"/api/users/{bob.Handle}");
        Assert.Equal(1, bobAnon!.FollowerCount);
        Assert.False(bobAnon.IsFollowedByCurrentUser);
    }

    [Fact]
    public async Task Following_feed_shows_followed_users_tweets_and_retweets_then_clears_on_unfollow()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_feed", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob_feed", "Bob");
        var carol = await RegisterAndLoginAsync(client, "@carol_feed", "Carol");

        // Carol posts a tweet that Bob will retweet (Alice does NOT follow Carol).
        var carolTweet = await CreateTweetAsync(client, carol.AccessToken, "Carol's original tweet.");

        // Alice follows Bob.
        await SendUserActionAsync(client, HttpMethod.Post, $"/api/users/{bob.Handle}/follow", alice.AccessToken);

        // Bob authors a tweet and retweets Carol's.
        var bobTweet = await CreateTweetAsync(client, bob.AccessToken, "Bob's own tweet.");
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{carolTweet.Id}/retweet", bob.AccessToken);

        // Alice's Following feed: Bob's tweet (authored) + Carol's tweet (surfaced by Bob's retweet).
        var feed = await GetFollowingFeedAsync(client, alice.AccessToken);

        var bobEntry = Assert.Single(feed.Items, t => t.Id == bobTweet.Id);
        Assert.Null(bobEntry.RetweetedBy); // an authored entry has no retweetedBy

        var retweetEntry = Assert.Single(feed.Items, t => t.Id == carolTweet.Id);
        Assert.NotNull(retweetEntry.RetweetedBy);
        Assert.Equal(bob.Handle, retweetEntry.RetweetedBy!.Handle); // surfaced by the followed user, Bob

        // After unfollowing Bob, neither his tweet nor his retweet appears (Alice follows no one Carol-ward).
        await SendUserActionAsync(client, HttpMethod.Delete, $"/api/users/{bob.Handle}/follow", alice.AccessToken);

        var clearedFeed = await GetFollowingFeedAsync(client, alice.AccessToken);
        Assert.DoesNotContain(clearedFeed.Items, t => t.Id == bobTweet.Id);
        Assert.DoesNotContain(clearedFeed.Items, t => t.Id == carolTweet.Id);
    }

    [Fact]
    public async Task Following_yourself_returns_400()
    {
        var client = _factory.CreateClient();
        var user = await RegisterAndLoginAsync(client, "@narcissus", "Narcissus");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{user.Handle}/follow");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Following_an_unknown_handle_returns_404()
    {
        var client = _factory.CreateClient();
        var user = await RegisterAndLoginAsync(client, "@seeker", "Seeker");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/@nobody-here/follow");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Getting_an_unknown_handle_returns_404()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/@does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Following_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/users/@anyone/follow", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Following_feed_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/feed/following");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Sends a follow/unfollow action as the given user and returns the target's updated profile.</summary>
    private static async Task<UserResponse> SendUserActionAsync(
        HttpClient client, HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        return user!;
    }

    /// <summary>Reads a user's lite profile as the given authenticated user (so the follow flag reflects them).</summary>
    private static async Task<UserResponse> GetUserAsAsync(HttpClient client, string handle, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/users/{handle}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        return user!;
    }

    /// <summary>Sends a tweet engagement action (e.g. retweet) and returns the updated tweet.</summary>
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

    /// <summary>Reads the Following feed as the given authenticated user.</summary>
    private static async Task<PagedTweets> GetFollowingFeedAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/feed/following?limit=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedTweets>();
        Assert.NotNull(page);
        return page!;
    }

    /// <summary>Posts a top-level tweet as the given user and returns the created read model.</summary>
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

    /// <summary>Registers a fresh user and logs them in, returning the access token, id, and handle.</summary>
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

    private record UserResponse(
        Guid Id,
        string Handle,
        string DisplayName,
        string? Bio,
        DateTime CreatedAtUtc,
        int FollowerCount,
        int FollowingCount,
        bool IsFollowedByCurrentUser);

    private record PagedTweets(List<TweetResponse> Items, string? NextCursor);

    private record TweetResponse(
        Guid Id,
        string Content,
        Guid AuthorId,
        string AuthorHandle,
        string AuthorDisplayName,
        DateTime CreatedAtUtc,
        Guid? ParentId,
        int ReplyCount,
        int LikeCount,
        int RetweetCount,
        bool LikedByCurrentUser,
        bool RetweetedByCurrentUser,
        RetweetedByResponse? RetweetedBy,
        List<TweetMediaResponse> Media);

    private record RetweetedByResponse(Guid UserId, string Handle, string DisplayName);

    private record TweetMediaResponse(string Url, string PublicId, int Position);
}
