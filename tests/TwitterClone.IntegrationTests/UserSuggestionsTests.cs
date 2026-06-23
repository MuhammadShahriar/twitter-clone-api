using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Integration tests for the "who to follow" suggestions endpoint (3B follow-up). Uses its own factory
/// instance (own in-memory DB), so the only users in play are the ones these tests create — letting the
/// follower-count ordering be asserted deterministically.
/// </summary>
public class UserSuggestionsTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public UserSuggestionsTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Suggestions_exclude_self_and_followed_ordered_by_follower_count()
    {
        var client = _factory.CreateClient();
        var a = await RegisterAndLoginAsync(client, "@a_sugg", "A");
        var b = await RegisterAndLoginAsync(client, "@b_sugg", "B");
        var c = await RegisterAndLoginAsync(client, "@c_sugg", "C");
        var d = await RegisterAndLoginAsync(client, "@d_sugg", "D");
        var e = await RegisterAndLoginAsync(client, "@e_sugg", "E");

        // Build follower counts: B=3 (c,d,e), C=2 (d,e), D=1 (e), E=0.
        await FollowAsync(client, c.AccessToken, b.Handle);
        await FollowAsync(client, d.AccessToken, b.Handle);
        await FollowAsync(client, e.AccessToken, b.Handle);
        await FollowAsync(client, d.AccessToken, c.Handle);
        await FollowAsync(client, e.AccessToken, c.Handle);
        await FollowAsync(client, e.AccessToken, d.Handle);

        // A already follows D, so D must not be suggested to A.
        await FollowAsync(client, a.AccessToken, d.Handle);

        var suggestions = await GetSuggestionsAsync(client, a.AccessToken, limit: 10);

        // Self (A) and the already-followed (D) are excluded.
        Assert.DoesNotContain(suggestions, u => u.Handle == a.Handle);
        Assert.DoesNotContain(suggestions, u => u.Handle == d.Handle);

        // The remaining users, most-followed first: B (3), C (2), E (0).
        Assert.Equal(new[] { b.Handle, c.Handle, e.Handle }, suggestions.Select(u => u.Handle).ToArray());
        Assert.Equal(3, suggestions[0].FollowerCount);
        Assert.Equal(2, suggestions[1].FollowerCount);
        Assert.Equal(0, suggestions[2].FollowerCount);

        // No avatar storage yet — the field is present but null.
        Assert.All(suggestions, u => Assert.Null(u.AvatarUrl));

        // Following B drops it out of the next call.
        await FollowAsync(client, a.AccessToken, b.Handle);
        var after = await GetSuggestionsAsync(client, a.AccessToken, limit: 10);
        Assert.DoesNotContain(after, u => u.Handle == b.Handle);
    }

    [Fact]
    public async Task Suggestions_respect_the_limit()
    {
        var client = _factory.CreateClient();
        var me = await RegisterAndLoginAsync(client, "@limit_me", "Me");
        for (var i = 0; i < 4; i++)
        {
            await RegisterAndLoginAsync(client, $"@limit_other{i}", $"Other {i}");
        }

        var suggestions = await GetSuggestionsAsync(client, me.AccessToken, limit: 2);

        Assert.Equal(2, suggestions.Count);
    }

    [Fact]
    public async Task Suggestions_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/suggestions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task FollowAsync(HttpClient client, string accessToken, string handle)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/users/{handle}/follow");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<List<UserSuggestionResponse>> GetSuggestionsAsync(
        HttpClient client, string accessToken, int limit)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/users/suggestions?limit={limit}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var suggestions = await response.Content.ReadFromJsonAsync<List<UserSuggestionResponse>>();
        Assert.NotNull(suggestions);
        return suggestions!;
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

    private record UserSuggestionResponse(
        Guid Id,
        string Handle,
        string DisplayName,
        string? AvatarUrl,
        string? Bio,
        int FollowerCount);
}
