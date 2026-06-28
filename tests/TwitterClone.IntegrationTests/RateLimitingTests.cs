using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Tests for the built-in rate limiter (Fix 3). Uses a factory that turns the limiter ON with small limits +
/// a short window (the rest of the suite runs with it OFF). Each test uses a unique <c>X-Forwarded-For</c> so
/// its per-IP auth partition is isolated from the others (the limiter reads XFF first). Asserts: hammering
/// login trips 429 and recovers after the window; a normal write rate isn't limited; reads aren't throttled.
/// </summary>
public class RateLimitingTests : IClassFixture<RateLimitedWebAppFactory>
{
    private readonly RateLimitedWebAppFactory _factory;

    public RateLimitingTests(RateLimitedWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Hammering_login_past_the_limit_returns_429_then_recovers()
    {
        var client = ClientFromIp("10.10.10.1");
        const string handle = "@rl_login";
        var email = await RegisterAsync(client, handle);

        // Hammer login well past the per-IP auth limit within the window — at least one must be throttled.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 12; i++)
        {
            statuses.Add((await LoginAsync(client, email)).StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);

        // After the window elapses, the limit resets and login works again.
        await Task.Delay(TimeSpan.FromSeconds(RateLimitedWebAppFactory.WindowSeconds + 1.5));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await LoginAsync(client, email)).StatusCode);
    }

    [Fact]
    public async Task A_normal_users_write_rate_is_not_limited()
    {
        var client = ClientFromIp("10.10.10.2");
        var token = await RegisterAndLoginAsync(client, "@rl_writer");

        // A handful of writes (the normal cadence) is comfortably under the per-user write cap — all succeed.
        for (var i = 0; i < 6; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await CreateTweetAsync(client, token, $"tweet {i}")).StatusCode);
        }
    }

    [Fact]
    public async Task Reads_are_not_throttled()
    {
        var client = ClientFromIp("10.10.10.3");
        var token = await RegisterAndLoginAsync(client, "@rl_reader");
        await CreateTweetAsync(client, token, "something to read");

        // Many reads in a tight loop are never throttled (GET is exempt from the limiter).
        for (var i = 0; i < 25; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/tweets?limit=10")).StatusCode);
        }
    }

    private HttpClient ClientFromIp(string ip)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
    }

    private static async Task<string> RegisterAsync(HttpClient client, string handle)
    {
        var email = $"{handle.TrimStart('@')}@example.com";
        var resp = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, handle, displayName = "RL", password = "P@ssw0rd!" });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return email;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password = "P@ssw0rd!" });

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string handle)
    {
        var email = await RegisterAsync(client, handle);
        var login = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<LoginBody>();
        return body!.AccessToken;
    }

    private static Task<HttpResponseMessage> CreateTweetAsync(HttpClient client, string token, string content)
    {
        var form = new MultipartFormDataContent { { new StringContent(content), "content" } };
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/tweets") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(request);
    }

    private record LoginBody(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Handle, string DisplayName);
}

/// <summary>
/// A <see cref="TestWebAppFactory"/> that re-enables the rate limiter with small limits and a short window
/// (overriding the suite-wide off switch), so the limiter can actually be exercised.
/// </summary>
public class RateLimitedWebAppFactory : TestWebAppFactory
{
    public const int WindowSeconds = 2;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Added after the base config, so these values win.
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Enabled"] = "true",
                ["RateLimiting:AuthPermitLimit"] = "5",
                ["RateLimiting:WritePermitLimit"] = "50",
                ["RateLimiting:WindowSeconds"] = WindowSeconds.ToString(),
            }));
    }
}
