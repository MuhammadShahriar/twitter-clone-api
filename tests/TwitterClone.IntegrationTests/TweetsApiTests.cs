using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Drives the real API end-to-end through <see cref="WebApplicationFactory{TEntryPoint}"/> (see
/// <see cref="TestWebAppFactory"/>).
///
/// The DbContext is re-pointed at the EF Core in-memory provider so the test runs in CI
/// with no live Postgres or Docker. Limitation: the in-memory provider is NOT relational —
/// it ignores SQL-level concerns (column types, max-length constraints, real transactions,
/// migrations, FK enforcement). It is enough to prove the auth→create→read HTTP spine; correctness
/// that depends on actual Postgres behaviour must be covered by tests against a real database.
///
/// As of Module 1D, creating a tweet requires a bearer token and the author is taken from that token —
/// so each create test first registers and logs a user in, then posts with the access token. As of
/// Module 2A the feed/replies are cursor-paginated (responses are <c>{ items, nextCursor }</c>), tweets
/// can be replies (a <c>parentId</c>), and an author can delete their own tweet. The factory is a class
/// fixture, so the in-memory DB is shared across the tests below — assertions are membership-based
/// (by id/content) rather than depending on absolute counts.
/// </summary>
public class TweetsApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public TweetsApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Post_then_get_returns_the_created_tweet_with_its_author()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@ada", "Ada Lovelace");

        var created = await CreateTweetAsync(client, author.AccessToken, "Hello, walking skeleton!");
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Hello, walking skeleton!", created.Content);
        Assert.Null(created.ParentId);
        Assert.Equal(0, created.ReplyCount);

        // The author comes from the token, not the request body.
        Assert.Equal(author.UserId, created.AuthorId);
        Assert.Equal("@ada", created.AuthorHandle);
        Assert.Equal("Ada Lovelace", created.AuthorDisplayName);

        // The detail route addresses the new resource and returns 200 (reads are public).
        var detail = await client.GetFromJsonAsync<TweetResponse>($"/api/tweets/{created.Id}");
        Assert.Equal("@ada", detail!.AuthorHandle);
        Assert.Equal("Ada Lovelace", detail.AuthorDisplayName);

        // GET /api/tweets -> paged feed contains the tweet we just created, with author info.
        var feed = await client.GetFromJsonAsync<PagedTweets>("/api/tweets?limit=50");
        Assert.NotNull(feed);
        Assert.Contains(feed!.Items, t =>
            t.Id == created.Id &&
            t.AuthorId == author.UserId &&
            t.AuthorHandle == "@ada" &&
            t.AuthorDisplayName == "Ada Lovelace");
    }

    [Fact]
    public async Task Post_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        // Sent as multipart (the content type the endpoint accepts) but with no bearer token.
        using var form = new MultipartFormDataContent { { new StringContent("I have no token."), "content" } };
        var response = await client.PostAsync("/api/tweets", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_feed_is_public()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/tweets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reply_appears_under_its_parent_and_not_in_the_feed()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@grace", "Grace Hopper");

        var parent = await CreateTweetAsync(client, author.AccessToken, "Parent tweet for the thread.");
        var reply = await CreateTweetAsync(client, author.AccessToken, "A threaded reply.", parent.Id);

        Assert.Equal(parent.Id, reply.ParentId);

        // The reply shows up under the parent via /replies.
        var replies = await client.GetFromJsonAsync<PagedTweets>($"/api/tweets/{parent.Id}/replies?limit=50");
        Assert.NotNull(replies);
        Assert.Contains(replies!.Items, t => t.Id == reply.Id && t.ParentId == parent.Id);

        // The parent reports the reply in its count.
        var parentDetail = await client.GetFromJsonAsync<TweetResponse>($"/api/tweets/{parent.Id}");
        Assert.Equal(1, parentDetail!.ReplyCount);

        // The feed is top-level only: the parent is there, the reply is not.
        var feed = await client.GetFromJsonAsync<PagedTweets>("/api/tweets?limit=50");
        Assert.Contains(feed!.Items, t => t.Id == parent.Id);
        Assert.DoesNotContain(feed.Items, t => t.Id == reply.Id);
    }

    [Fact]
    public async Task Replying_to_a_missing_parent_returns_400()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@katherine", "Katherine Johnson");

        using var form = new MultipartFormDataContent
        {
            { new StringContent("Reply to nobody."), "content" },
            { new StringContent(Guid.NewGuid().ToString()), "parentId" },
        };
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tweets") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", author.AccessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cursor_pagination_returns_a_next_cursor_and_does_not_repeat_items()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@margaret", "Margaret Hamilton");

        // Guarantee at least three top-level tweets exist so a limit of 2 spans more than one page.
        await CreateTweetAsync(client, author.AccessToken, "Page test tweet 1.");
        await CreateTweetAsync(client, author.AccessToken, "Page test tweet 2.");
        await CreateTweetAsync(client, author.AccessToken, "Page test tweet 3.");

        var firstPage = await client.GetFromJsonAsync<PagedTweets>("/api/tweets?limit=2");
        Assert.NotNull(firstPage);
        Assert.Equal(2, firstPage!.Items.Count);
        Assert.False(string.IsNullOrEmpty(firstPage.NextCursor)); // more results -> a cursor

        var secondPage = await client.GetFromJsonAsync<PagedTweets>(
            $"/api/tweets?limit=2&cursor={Uri.EscapeDataString(firstPage.NextCursor!)}");
        Assert.NotNull(secondPage);
        Assert.NotEmpty(secondPage!.Items);

        // The second page must not repeat any id from the first — stable, non-overlapping paging.
        var firstIds = firstPage.Items.Select(t => t.Id).ToHashSet();
        Assert.DoesNotContain(secondPage.Items, t => firstIds.Contains(t.Id));
    }

    [Fact]
    public async Task Author_can_delete_their_own_tweet()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@radia", "Radia Perlman");

        var created = await CreateTweetAsync(client, author.AccessToken, "This tweet will be deleted.");

        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/tweets/{created.Id}");
        delete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", author.AccessToken);
        var deleteResponse = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // It's really gone.
        var getResponse = await client.GetAsync($"/api/tweets/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Deleting_another_users_tweet_returns_403()
    {
        var client = _factory.CreateClient();
        var owner = await RegisterAndLoginAsync(client, "@barbara", "Barbara Liskov");
        var intruder = await RegisterAndLoginAsync(client, "@frances", "Frances Allen");

        var created = await CreateTweetAsync(client, owner.AccessToken, "Owned by Barbara.");

        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/tweets/{created.Id}");
        delete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", intruder.AccessToken);
        var deleteResponse = await client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        // Still there — the forbidden delete didn't remove it.
        var getResponse = await client.GetAsync($"/api/tweets/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_missing_tweet_returns_404()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@joan", "Joan Clarke");

        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/tweets/{Guid.NewGuid()}");
        delete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", author.AccessToken);

        var deleteResponse = await client.SendAsync(delete);

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/tweets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_images_stores_and_returns_media()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@hedy", "Hedy Lamarr");

        using var form = new MultipartFormDataContent { { new StringContent("A tweet with a picture."), "content" } };
        form.Add(ImageContent(new byte[] { 1, 2, 3, 4 }, "image/png"), "images", "cat.png");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tweets") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", author.AccessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TweetResponse>();
        Assert.NotNull(created);
        var media = Assert.Single(created!.Media);
        Assert.Equal("fake/cat.png", media.PublicId);
        Assert.Equal(0, media.Position);
        Assert.False(string.IsNullOrEmpty(media.Url));

        // The media survives a read-back through the public detail route.
        var detail = await client.GetFromJsonAsync<TweetResponse>($"/api/tweets/{created.Id}");
        Assert.Single(detail!.Media);
        Assert.Equal("fake/cat.png", detail.Media[0].PublicId);
    }

    [Fact]
    public async Task Post_image_only_tweet_is_allowed()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@dorothy", "Dorothy Vaughan");

        // No content field at all — just an image. Twitter allows image-only tweets.
        using var form = new MultipartFormDataContent();
        form.Add(ImageContent(new byte[] { 1, 2, 3, 4 }, "image/png"), "images", "photo.png");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tweets") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", author.AccessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TweetResponse>();
        Assert.NotNull(created);
        Assert.Equal(string.Empty, created!.Content);
        Assert.Single(created.Media);
    }

    [Fact]
    public async Task Post_with_more_than_four_images_returns_400()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@annie", "Annie Easley");

        using var form = new MultipartFormDataContent { { new StringContent("Too many pictures."), "content" } };
        for (var i = 0; i < 5; i++)
        {
            form.Add(ImageContent(new byte[] { 1, 2, 3 }, "image/png"), "images", $"img{i}.png");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tweets") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", author.AccessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_a_disallowed_image_type_returns_400()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@evelyn", "Evelyn Boyd");

        using var form = new MultipartFormDataContent { { new StringContent("Not an image."), "content" } };
        form.Add(ImageContent(new byte[] { 1, 2, 3 }, "application/pdf"), "images", "doc.pdf");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tweets") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", author.AccessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Like_then_unlike_updates_count_and_flag_idempotently()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@linus", "Linus Torvalds");

        var tweet = await CreateTweetAsync(client, author.AccessToken, "Like me.");
        Assert.Equal(0, tweet.LikeCount);
        Assert.False(tweet.LikedByCurrentUser);

        // Like -> count 1, flag true.
        var liked = await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{tweet.Id}/like", author.AccessToken);
        Assert.Equal(1, liked.LikeCount);
        Assert.True(liked.LikedByCurrentUser);

        // Liking again is idempotent -> still 1.
        var likedAgain = await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{tweet.Id}/like", author.AccessToken);
        Assert.Equal(1, likedAgain.LikeCount);
        Assert.True(likedAgain.LikedByCurrentUser);

        // Unlike -> count 0, flag false.
        var unliked = await SendTweetActionAsync(client, HttpMethod.Delete, $"/api/tweets/{tweet.Id}/like", author.AccessToken);
        Assert.Equal(0, unliked.LikeCount);
        Assert.False(unliked.LikedByCurrentUser);

        // Unliking again is idempotent -> still 0.
        var unlikedAgain = await SendTweetActionAsync(client, HttpMethod.Delete, $"/api/tweets/{tweet.Id}/like", author.AccessToken);
        Assert.Equal(0, unlikedAgain.LikeCount);
        Assert.False(unlikedAgain.LikedByCurrentUser);
    }

    [Fact]
    public async Task Retweet_then_unretweet_updates_count_and_flag_idempotently()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@ken", "Ken Thompson");

        var tweet = await CreateTweetAsync(client, author.AccessToken, "Retweet me.");
        Assert.Equal(0, tweet.RetweetCount);
        Assert.False(tweet.RetweetedByCurrentUser);

        var retweeted = await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{tweet.Id}/retweet", author.AccessToken);
        Assert.Equal(1, retweeted.RetweetCount);
        Assert.True(retweeted.RetweetedByCurrentUser);

        // Idempotent re-retweet.
        var retweetedAgain = await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{tweet.Id}/retweet", author.AccessToken);
        Assert.Equal(1, retweetedAgain.RetweetCount);

        var unretweeted = await SendTweetActionAsync(client, HttpMethod.Delete, $"/api/tweets/{tweet.Id}/retweet", author.AccessToken);
        Assert.Equal(0, unretweeted.RetweetCount);
        Assert.False(unretweeted.RetweetedByCurrentUser);

        // Idempotent re-unretweet.
        var unretweetedAgain = await SendTweetActionAsync(client, HttpMethod.Delete, $"/api/tweets/{tweet.Id}/retweet", author.AccessToken);
        Assert.Equal(0, unretweetedAgain.RetweetCount);
    }

    [Fact]
    public async Task Likes_count_for_everyone_but_the_flag_is_per_user()
    {
        var client = _factory.CreateClient();
        var liker = await RegisterAndLoginAsync(client, "@dennis", "Dennis Ritchie");
        var other = await RegisterAndLoginAsync(client, "@brian", "Brian Kernighan");

        var tweet = await CreateTweetAsync(client, liker.AccessToken, "One like, two viewers.");
        await SendTweetActionAsync(client, HttpMethod.Post, $"/api/tweets/{tweet.Id}/like", liker.AccessToken);

        // The liker sees their own flag set.
        var asLiker = await GetTweetAsAsync(client, tweet.Id, liker.AccessToken);
        Assert.Equal(1, asLiker.LikeCount);
        Assert.True(asLiker.LikedByCurrentUser);

        // A different authenticated user sees the same count but their own flag is false.
        var asOther = await GetTweetAsAsync(client, tweet.Id, other.AccessToken);
        Assert.Equal(1, asOther.LikeCount);
        Assert.False(asOther.LikedByCurrentUser);

        // An anonymous reader sees the count but no flag.
        var asAnon = await client.GetFromJsonAsync<TweetResponse>($"/api/tweets/{tweet.Id}");
        Assert.Equal(1, asAnon!.LikeCount);
        Assert.False(asAnon.LikedByCurrentUser);
    }

    [Fact]
    public async Task Liking_a_missing_tweet_returns_404()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@bjarne", "Bjarne Stroustrup");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tweets/{Guid.NewGuid()}/like");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", author.AccessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Liking_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/tweets/{Guid.NewGuid()}/like", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Retweeting_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/api/tweets/{Guid.NewGuid()}/retweet", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Sends a like/unlike/retweet/unretweet action as the given user and returns the updated tweet.</summary>
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

    /// <summary>Reads a tweet's detail as the given authenticated user (so per-user flags reflect them).</summary>
    private static async Task<TweetResponse> GetTweetAsAsync(HttpClient client, Guid id, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/tweets/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tweet = await response.Content.ReadFromJsonAsync<TweetResponse>();
        Assert.NotNull(tweet);
        return tweet!;
    }

    /// <summary>Builds a multipart file part with the given bytes and content type.</summary>
    private static ByteArrayContent ImageContent(byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
    }

    /// <summary>Posts a tweet (optionally a reply) as the given user and returns the created read model.</summary>
    private static async Task<TweetResponse> CreateTweetAsync(
        HttpClient client, string accessToken, string content, Guid? parentId = null)
    {
        // POST /api/tweets is multipart/form-data (it can carry image files); send just the text fields here.
        using var form = new MultipartFormDataContent { { new StringContent(content), "content" } };
        if (parentId.HasValue)
        {
            form.Add(new StringContent(parentId.Value.ToString()), "parentId");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tweets") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<TweetResponse>();
        Assert.NotNull(created);
        return created!;
    }

    /// <summary>Registers a fresh user and logs them in, returning the access token and user id.</summary>
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
        return new AuthedUser(auth!.AccessToken, auth.UserId);
    }

    private record AuthedUser(string AccessToken, Guid UserId);

    private record LoginBody(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Handle, string DisplayName);

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
        List<TweetMediaResponse> Media);

    private record TweetMediaResponse(string Url, string PublicId, int Position);
}
