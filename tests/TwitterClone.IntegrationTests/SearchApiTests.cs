using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// HTTP-level integration tests for Module 8A: the search endpoints
/// (<c>GET /api/search/users</c> and <c>/api/search/tweets</c>). Drives the real API end-to-end
/// (<see cref="TestWebAppFactory"/>, EF Core in-memory provider). Both are public, case-insensitive, return a
/// blank-query empty page, and carry the caller's flags. Fresh handles per test (shared in-memory DB);
/// membership asserted by id/handle.
/// </summary>
public class SearchApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public SearchApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task User_search_finds_by_handle_and_display_name_with_followback_flag()
    {
        var client = _factory.CreateClient();
        var viewer = await RegisterAndLoginAsync(client, "@srch_api_viewer", "Viewer");
        var needle = await RegisterAndLoginAsync(client, "@zephyrine_api", "Zephyrine");

        // The viewer follows the target (so the result row is flagged followed-by-viewer).
        await FollowAsync(client, viewer.AccessToken, needle.Handle);

        // Matches a distinctive handle substring, case-insensitively, as the viewer.
        var byHandle = await SearchUsersAsAsync(client, "ZEPHYR", viewer.AccessToken);
        var row = Assert.Single(byHandle.Items, u => u.Handle == needle.Handle);
        Assert.True(row.IsFollowedByCurrentUser);

        // Also findable by display-name substring.
        var byName = await SearchUsersAsAsync(client, "zephyrine", viewer.AccessToken);
        Assert.Contains(byName.Items, u => u.Handle == needle.Handle);
    }

    [Fact]
    public async Task Tweet_search_finds_by_content_and_is_public()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@srch_api_author", "Author");

        var marker = $"quokka{Guid.NewGuid():N}";
        await CreateTweetAsync(client, author.AccessToken, $"A wild {marker} appeared");

        // Public (no token), case-insensitive content match.
        var page = await client.GetFromJsonAsync<PagedTweets>($"/api/search/tweets?q={marker.ToUpperInvariant()}");
        Assert.NotNull(page);
        var hit = Assert.Single(page!.Items);
        Assert.Contains(marker, hit.Content);
        Assert.False(hit.LikedByCurrentUser); // anonymous reader
    }

    [Fact]
    public async Task Blank_query_returns_an_empty_page()
    {
        var client = _factory.CreateClient();

        var users = await client.GetFromJsonAsync<PagedUsers>("/api/search/users?q=%20%20");
        Assert.NotNull(users);
        Assert.Empty(users!.Items);
        Assert.Null(users.NextCursor);

        var tweets = await client.GetFromJsonAsync<PagedTweets>("/api/search/tweets");
        Assert.NotNull(tweets);
        Assert.Empty(tweets!.Items);
        Assert.Null(tweets.NextCursor);
    }

    [Fact]
    public async Task Tweet_search_paginates_without_duplicates_or_skips()
    {
        var client = _factory.CreateClient();
        var author = await RegisterAndLoginAsync(client, "@srch_api_pg", "Author");

        var marker = $"narwhal{Guid.NewGuid():N}";
        for (var i = 0; i < 5; i++)
        {
            await CreateTweetAsync(client, author.AccessToken, $"{marker} number {i}");
        }

        var oneShot = await client.GetFromJsonAsync<PagedTweets>($"/api/search/tweets?q={marker}&limit=50");
        var canonical = oneShot!.Items.Select(t => t.Id).ToList();
        Assert.Equal(5, canonical.Count);

        var paged = new List<Guid>();
        string? cursor = null;
        for (var guard = 0; guard < 100; guard++)
        {
            var url = $"/api/search/tweets?q={marker}&limit=2{(cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}")}";
            var page = await client.GetFromJsonAsync<PagedTweets>(url);
            paged.AddRange(page!.Items.Select(t => t.Id));
            if (page.NextCursor is null)
            {
                break;
            }

            cursor = page.NextCursor;
        }

        Assert.Equal(canonical, paged);
        Assert.Equal(canonical.Count, paged.Distinct().Count());
    }

    private static async Task FollowAsync(HttpClient client, string accessToken, string handle)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{handle}/follow");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<PagedUsers> SearchUsersAsAsync(HttpClient client, string q, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/search/users?q={Uri.EscapeDataString(q)}&limit=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedUsers>();
        Assert.NotNull(page);
        return page!;
    }

    private static async Task CreateTweetAsync(HttpClient client, string accessToken, string content)
    {
        using var form = new MultipartFormDataContent { { new StringContent(content), "content" } };
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tweets") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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

    private record PagedUsers(List<UserItem> Items, string? NextCursor);

    private record UserItem(Guid Id, string Handle, string DisplayName, bool IsFollowedByCurrentUser);

    private record PagedTweets(List<TweetItem> Items, string? NextCursor);

    private record TweetItem(Guid Id, string Content, bool LikedByCurrentUser);
}
