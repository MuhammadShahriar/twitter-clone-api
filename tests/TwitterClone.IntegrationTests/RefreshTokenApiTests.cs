using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Exercises the refresh-token lifecycle: cookie issuance on login, rotation on refresh, reuse
/// detection (family revocation), and logout. Cookies are managed manually (HandleCookies = false)
/// so a test can deliberately replay an old token to trigger reuse detection.
/// </summary>
public class RefreshTokenApiTests : IClassFixture<TestWebAppFactory>
{
    private const string CookieName = "refresh_token";
    private readonly TestWebAppFactory _factory;

    public RefreshTokenApiTests(TestWebAppFactory factory) => _factory = factory;

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    [Fact]
    public async Task Login_sets_an_httponly_refresh_cookie()
    {
        var client = CreateClient();
        var login = await RegisterAndLoginAsync(client, "@neo", "neo@example.com");

        Assert.True(login.Headers.Contains("Set-Cookie"));
        var setCookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith($"{CookieName}="));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrEmpty(ReadCookie(login)));
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_returns_a_new_access_token()
    {
        var client = CreateClient();
        var login = await RegisterAndLoginAsync(client, "@trinity", "trinity@example.com");
        var firstToken = ReadCookie(login);

        var refresh = await PostWithCookie(client, "/api/auth/refresh", firstToken);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var rotatedToken = ReadCookie(refresh);
        Assert.False(string.IsNullOrEmpty(rotatedToken));
        Assert.NotEqual(firstToken, rotatedToken); // rotation issues a brand-new token

        // The rotated token works for a subsequent refresh.
        var refreshAgain = await PostWithCookie(client, "/api/auth/refresh", rotatedToken);
        Assert.Equal(HttpStatusCode.OK, refreshAgain.StatusCode);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_is_rejected_and_revokes_the_family()
    {
        var client = CreateClient();
        var login = await RegisterAndLoginAsync(client, "@morpheus", "morpheus@example.com");
        var firstToken = ReadCookie(login);

        // Rotate once: firstToken is now revoked, newToken is active.
        var refresh = await PostWithCookie(client, "/api/auth/refresh", firstToken);
        var newToken = ReadCookie(refresh);

        // Replay the OLD token → reuse detected → 401.
        var reuse = await PostWithCookie(client, "/api/auth/refresh", firstToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        // Family is now revoked, so even the previously-valid rotated token no longer works.
        var afterRevoke = await PostWithCookie(client, "/api/auth/refresh", newToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_token_and_clears_the_cookie()
    {
        var client = CreateClient();
        var login = await RegisterAndLoginAsync(client, "@cypher", "cypher@example.com");
        var token = ReadCookie(login);

        var logout = await PostWithCookie(client, "/api/auth/logout", token);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // The logout response clears the cookie (empty value / past expiry).
        var clearing = logout.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith($"{CookieName}="));
        Assert.StartsWith($"{CookieName}=;", clearing);

        // The revoked token can no longer be refreshed.
        var refresh = await PostWithCookie(client, "/api/auth/refresh", token);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_without_a_cookie_returns_401()
    {
        var client = CreateClient();
        var response = await client.PostAsync("/api/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> RegisterAndLoginAsync(
        HttpClient client, string handle, string email)
    {
        const string password = "P@ssw0rd!";
        await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, handle, displayName = "Test User", password });

        return await client.PostAsJsonAsync("/api/auth/login", new { email, password });
    }

    private static Task<HttpResponseMessage> PostWithCookie(HttpClient client, string url, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (token is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={token}");
        }

        return client.SendAsync(request);
    }

    /// <summary>Reads the refresh-token value from a response's Set-Cookie header.</summary>
    private static string? ReadCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        var header = cookies.FirstOrDefault(c => c.StartsWith($"{CookieName}="));
        if (header is null)
        {
            return null;
        }

        var firstPair = header.Split(';')[0];          // "refresh_token=<value>"
        var value = firstPair[(firstPair.IndexOf('=') + 1)..];
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
