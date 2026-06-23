using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Exercises the register → login spine and asserts the issued JWT carries the expected claims.
/// Runs entirely on the in-memory provider with test-supplied JWT settings (see
/// <see cref="TestWebAppFactory"/>).
/// </summary>
public class AuthApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public AuthApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_then_login_returns_a_jwt_carrying_the_user_id_and_handle()
    {
        var client = _factory.CreateClient();

        const string email = "ada@example.com";
        const string handle = "@ada";
        const string password = "P@ssw0rd!";

        // Register -> 200 with the created account.
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, handle, displayName = "Ada Lovelace", password });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterBody>();
        Assert.NotNull(registered);
        Assert.NotEqual(Guid.Empty, registered!.UserId);
        Assert.Equal(handle, registered.Handle);

        // Login -> 200 with a JWT.
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var auth = await loginResponse.Content.ReadFromJsonAsync<LoginBody>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));
        Assert.Equal(registered.UserId, auth.UserId);

        // The token's claims should match the authenticated user.
        var claims = DecodeJwtPayload(auth.AccessToken);
        Assert.Equal(auth.UserId.ToString(), claims["sub"].GetString());
        Assert.Equal(handle, claims["handle"].GetString());
        Assert.Equal(email, claims["email"].GetString());
        Assert.True(claims.ContainsKey("exp"));
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var client = _factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "grace@example.com", handle = "@grace", displayName = "Grace Hopper", password = "P@ssw0rd!" });

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "grace@example.com", password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Me_with_a_valid_bearer_token_returns_the_current_user()
    {
        var client = _factory.CreateClient();

        const string email = "linus@example.com";
        const string handle = "@linus";
        const string password = "P@ssw0rd!";

        await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, handle, displayName = "Linus Torvalds", password });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var auth = await loginResponse.Content.ReadFromJsonAsync<LoginBody>();

        // Attach the bearer token and call the protected endpoint.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var meResponse = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = await meResponse.Content.ReadFromJsonAsync<MeBody>();
        Assert.NotNull(me);
        Assert.Equal(auth.UserId, me!.UserId);
        Assert.Equal(handle, me.Handle);
        Assert.Equal(email, me.Email);
        Assert.Equal("Linus Torvalds", me.DisplayName);
    }

    [Fact]
    public async Task Me_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Repeated_failed_logins_lock_the_account()
    {
        var client = _factory.CreateClient();

        const string email = "lockme@example.com";
        const string password = "P@ssw0rd!";

        await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, handle = "@lockme", displayName = "Lock Me", password });

        // Lockout threshold is 5 failed attempts. Burn through them with the wrong password.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });
        }

        // The account is now locked: even the CORRECT password is refused with 423 Locked.
        var lockedResponse = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Locked, lockedResponse.StatusCode);
    }

    [Fact]
    public async Task Registering_a_duplicate_handle_returns_400()
    {
        var client = _factory.CreateClient();

        var first = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "first@example.com", handle = "@dup", displayName = "First", password = "P@ssw0rd!" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Same handle, different email -> the handle pre-check rejects it as a clean 400 (not a 500).
        var second = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email = "second@example.com", handle = "@dup", displayName = "Second", password = "P@ssw0rd!" });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public void Startup_fails_fast_when_jwt_secret_is_too_short()
    {
        // A misconfigured deploy (missing/short signing key) must fail to boot with a clear message,
        // not surface as an opaque runtime 500 on the first login.
        using var factory = new ShortJwtKeyFactory();

        var ex = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(ex);
        Assert.Contains("SecretKey", ex!.ToString());
    }

    /// <summary>A factory that overrides the (valid) test JWT secret with one that is too short.</summary>
    private sealed class ShortJwtKeyFactory : TestWebAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // 9 bytes — below the 32-byte (256-bit) minimum enforced by ValidateOnStart.
                    ["Jwt:SecretKey"] = "too-short",
                }));
        }
    }

    private record RegisterBody(Guid UserId, string Email, string Handle, string DisplayName);

    private record MeBody(Guid UserId, string Email, string Handle, string DisplayName);

    private record LoginBody(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Handle, string DisplayName);

    /// <summary>Decodes a JWT's payload (the middle segment) into its claim set, no signing key needed.</summary>
    private static Dictionary<string, JsonElement> DecodeJwtPayload(string jwt)
    {
        var payload = jwt.Split('.')[1];

        // Base64Url -> Base64: restore padding and the URL-unsafe characters.
        var base64 = payload.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
    }
}
