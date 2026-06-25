using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// HTTP-level integration tests for Module 7A: the follower / following list endpoints
/// (<c>GET /api/users/{handle}/followers</c> and <c>/following</c>). Drives the real API end-to-end
/// (<see cref="TestWebAppFactory"/>, EF Core in-memory provider). Both lists are public, carry the caller's
/// <c>isFollowedByCurrentUser</c> flag (so you can follow back), 404 on an unknown handle, and work without a
/// token. Fresh handles per test (shared in-memory DB); membership asserted by id.
/// </summary>
public class FollowListsApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public FollowListsApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Followers_and_following_lists_reflect_the_graph_with_followback_flag()
    {
        var client = _factory.CreateClient();
        var target = await RegisterAndLoginAsync(client, "@fl_api_target", "Target");
        var fan = await RegisterAndLoginAsync(client, "@fl_api_fan", "Fan");
        var idol = await RegisterAndLoginAsync(client, "@fl_api_idol", "Idol");
        var viewer = await RegisterAndLoginAsync(client, "@fl_api_viewer", "Viewer");

        // fan follows target; target follows idol. The viewer also follows the fan (for the follow-back flag).
        await FollowAsync(client, fan.AccessToken, target.Handle);
        await FollowAsync(client, target.AccessToken, idol.Handle);
        await FollowAsync(client, viewer.AccessToken, fan.Handle);

        // target's followers (as the viewer): contains the fan, flagged followed-by-viewer (viewer follows fan).
        var followers = await GetListAsAsync(client, $"/api/users/{target.Handle}/followers", viewer.AccessToken);
        var fanRow = Assert.Single(followers.Items, u => u.Handle == fan.Handle);
        Assert.True(fanRow.IsFollowedByCurrentUser);

        // target's following: contains the idol; the viewer does not follow the idol -> flag false.
        var following = await GetListAsAsync(client, $"/api/users/{target.Handle}/following", viewer.AccessToken);
        var idolRow = Assert.Single(following.Items, u => u.Handle == idol.Handle);
        Assert.False(idolRow.IsFollowedByCurrentUser);

        // The fan is NOT in target's following, and the idol is NOT in target's followers.
        Assert.DoesNotContain(following.Items, u => u.Handle == fan.Handle);
        Assert.DoesNotContain(followers.Items, u => u.Handle == idol.Handle);
    }

    [Fact]
    public async Task Lists_are_public_and_work_without_a_token()
    {
        var client = _factory.CreateClient();
        var target = await RegisterAndLoginAsync(client, "@fl_pub_target", "Target");
        var fan = await RegisterAndLoginAsync(client, "@fl_pub_fan", "Fan");

        await FollowAsync(client, fan.AccessToken, target.Handle);

        // No Authorization header: still 200, and the anonymous reader's follow-back flag is false.
        var followers = await client.GetFromJsonAsync<PagedUsers>($"/api/users/{target.Handle}/followers");
        Assert.NotNull(followers);
        var fanRow = Assert.Single(followers!.Items, u => u.Handle == fan.Handle);
        Assert.False(fanRow.IsFollowedByCurrentUser);

        var following = await client.GetFromJsonAsync<PagedUsers>($"/api/users/{fan.Handle}/following");
        Assert.NotNull(following);
        Assert.Single(following!.Items, u => u.Handle == target.Handle);
    }

    [Fact]
    public async Task Unknown_handle_is_404()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/users/@nobody_here/followers")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/users/@nobody_here/following")).StatusCode);
    }

    [Fact]
    public async Task Lists_paginate_without_duplicates_or_skips()
    {
        var client = _factory.CreateClient();
        var target = await RegisterAndLoginAsync(client, "@fl_pg_target", "Target");

        // Five followers.
        var followers = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var f = await RegisterAndLoginAsync(client, $"@fl_pg_f{i}", $"F{i}");
            await FollowAsync(client, f.AccessToken, target.Handle);
            followers.Add(f.Handle);
        }

        // One-shot list (the canonical order).
        var oneShot = await client.GetFromJsonAsync<PagedUsers>($"/api/users/{target.Handle}/followers?limit=50");
        var canonical = oneShot!.Items.Select(u => u.Handle).ToList();
        Assert.Equal(5, canonical.Count);
        Assert.All(followers, h => Assert.Contains(h, canonical));

        // Walk it two at a time; the concatenation must equal the one-shot order, no dupes/skips.
        var paged = new List<string>();
        string? cursor = null;
        for (var guard = 0; guard < 100; guard++)
        {
            var url = $"/api/users/{target.Handle}/followers?limit=2{(cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}")}";
            var page = await client.GetFromJsonAsync<PagedUsers>(url);
            paged.AddRange(page!.Items.Select(u => u.Handle));
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

    private static async Task<PagedUsers> GetListAsAsync(HttpClient client, string path, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{path}?limit=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedUsers>();
        Assert.NotNull(page);
        return page!;
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

    private record UserItem(
        Guid Id,
        string Handle,
        string DisplayName,
        string? Bio,
        string? AvatarUrl,
        DateTime CreatedAtUtc,
        int FollowerCount,
        int FollowingCount,
        bool IsFollowedByCurrentUser);
}
