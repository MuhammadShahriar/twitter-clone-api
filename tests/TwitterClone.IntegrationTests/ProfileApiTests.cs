using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TwitterClone.Application.Common.Interfaces;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// HTTP-spine tests for the Module 4A profile backend: editing the display name/bio, uploading an avatar
/// (the Cloudinary call is replaced by <see cref="FakeImageStorageService"/>), and the per-user tweet and
/// likes timelines. Runs over the EF Core in-memory provider via <see cref="TestWebAppFactory"/> — enough to
/// prove the auth→write→read spine; the timelines' SQL translation is covered by
/// <see cref="UserTimelinesSqliteTests"/>.
/// </summary>
public class ProfileApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ProfileApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Edit_profile_updates_display_name_and_bio_and_is_visible_on_the_public_profile()
    {
        var client = _factory.CreateClient();
        var user = await RegisterAndLoginAsync(client, "@edit_me", "Original Name");

        var updated = await UpdateProfileAsync(client, user.AccessToken, "New Name", "A fresh bio.");
        Assert.Equal("New Name", updated.DisplayName);
        Assert.Equal("A fresh bio.", updated.Bio);

        // The public profile reflects the edit.
        var profile = await client.GetFromJsonAsync<UserResponse>($"/api/users/{user.Handle}");
        Assert.Equal("New Name", profile!.DisplayName);
        Assert.Equal("A fresh bio.", profile.Bio);
    }

    [Fact]
    public async Task Avatar_upload_sets_the_avatar_url_everywhere_it_surfaces()
    {
        var client = _factory.CreateClient();
        var user = await RegisterAndLoginAsync(client, "@avatar_me", "Avatar User");

        // Before upload the avatar is null on the public profile.
        var before = await client.GetFromJsonAsync<UserResponse>($"/api/users/{user.Handle}");
        Assert.Null(before!.AvatarUrl);

        var updated = await UploadAvatarAsync(client, user.AccessToken, "me.png", "image/png");
        // FakeImageStorageService returns a deterministic, filename-derived URL.
        Assert.Equal("https://images.test/me.png", updated.AvatarUrl);

        // The public profile now carries the avatar.
        var profile = await client.GetFromJsonAsync<UserResponse>($"/api/users/{user.Handle}");
        Assert.Equal("https://images.test/me.png", profile!.AvatarUrl);

        // So does GET /api/auth/me for the logged-in user.
        var me = await GetAsAsync<CurrentUserResponse>(client, "/api/auth/me", user.AccessToken);
        Assert.Equal("https://images.test/me.png", me.AvatarUrl);
    }

    [Fact]
    public async Task Replacing_an_avatar_deletes_the_previous_image_from_the_host()
    {
        var client = _factory.CreateClient();
        var user = await RegisterAndLoginAsync(client, "@replace_avatar", "Replace Avatar");

        // First upload (publicId "fake/first-avatar.png"), then replace it with a second image.
        await UploadAvatarAsync(client, user.AccessToken, "first-avatar.png", "image/png");
        var replaced = await UploadAvatarAsync(client, user.AccessToken, "second-avatar.png", "image/png");
        Assert.Equal("https://images.test/second-avatar.png", replaced.AvatarUrl);

        // The replace best-effort deleted the OLD asset by its public id (and not the new one).
        var fake = (FakeImageStorageService)_factory.Services.GetRequiredService<IImageStorageService>();
        Assert.Contains("fake/first-avatar.png", fake.DeletedPublicIds);
        Assert.DoesNotContain("fake/second-avatar.png", fake.DeletedPublicIds);
    }

    [Fact]
    public async Task Delete_avatar_clears_it_everywhere_deletes_the_asset_and_is_idempotent()
    {
        var client = _factory.CreateClient();
        var user = await RegisterAndLoginAsync(client, "@clear_avatar", "Clear Avatar");

        await UploadAvatarAsync(client, user.AccessToken, "to-clear.png", "image/png");

        // DELETE clears the avatar and returns the refreshed (avatar-less) profile.
        var afterDelete = await DeleteAvatarAsync(client, user.AccessToken);
        Assert.Null(afterDelete.AvatarUrl);

        // The public profile reflects the cleared avatar.
        var profile = await client.GetFromJsonAsync<UserResponse>($"/api/users/{user.Handle}");
        Assert.Null(profile!.AvatarUrl);

        // The old asset was deleted from the host.
        var fake = (FakeImageStorageService)_factory.Services.GetRequiredService<IImageStorageService>();
        Assert.Contains("fake/to-clear.png", fake.DeletedPublicIds);

        // Idempotent: deleting again (now there's no avatar) still returns 200 with a null avatar.
        var again = await DeleteAvatarAsync(client, user.AccessToken);
        Assert.Null(again.AvatarUrl);
    }

    [Fact]
    public async Task Delete_avatar_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/users/me/avatar");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task User_tweets_timeline_returns_top_level_tweets_newest_first_excluding_replies()
    {
        var client = _factory.CreateClient();
        var user = await RegisterAndLoginAsync(client, "@timeline_me", "Timeline User");

        var first = await CreateTweetAsync(client, user.AccessToken, "My first tweet.");
        var second = await CreateTweetAsync(client, user.AccessToken, "My second tweet.");
        // A reply authored by the same user — must NOT appear in their Tweets timeline.
        var reply = await CreateTweetAsync(client, user.AccessToken, "A reply to my own tweet.", first.Id);

        var page = await client.GetFromJsonAsync<PagedTweets>($"/api/users/{user.Handle}/tweets?limit=50");
        Assert.NotNull(page);

        var ids = page!.Items.Select(t => t.Id).ToList();
        Assert.Equal(new[] { second.Id, first.Id }, ids.ToArray());
        Assert.DoesNotContain(reply.Id, ids);
    }

    [Fact]
    public async Task User_likes_timeline_returns_liked_tweets()
    {
        var client = _factory.CreateClient();
        var owner = await RegisterAndLoginAsync(client, "@likes_owner", "Owner");
        var liker = await RegisterAndLoginAsync(client, "@likes_liker", "Liker");

        var liked = await CreateTweetAsync(client, owner.AccessToken, "Worth a like.");
        var notLiked = await CreateTweetAsync(client, owner.AccessToken, "Ignored.");

        await SendActionAsync(client, HttpMethod.Post, $"/api/tweets/{liked.Id}/like", liker.AccessToken);

        var page = await client.GetFromJsonAsync<PagedTweets>($"/api/users/{liker.Handle}/likes?limit=50");
        Assert.NotNull(page);
        Assert.Contains(page!.Items, t => t.Id == liked.Id);
        Assert.DoesNotContain(page.Items, t => t.Id == notLiked.Id);
    }

    [Fact]
    public async Task Timelines_for_an_unknown_handle_return_404()
    {
        var client = _factory.CreateClient();

        var tweets = await client.GetAsync("/api/users/@nobody_here/tweets");
        Assert.Equal(HttpStatusCode.NotFound, tweets.StatusCode);

        var likes = await client.GetAsync("/api/users/@nobody_here/likes");
        Assert.Equal(HttpStatusCode.NotFound, likes.StatusCode);
    }

    [Fact]
    public async Task Edit_profile_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/users/me", new { displayName = "Nope", bio = (string?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Avatar_upload_without_a_token_returns_401()
    {
        var client = _factory.CreateClient();

        using var form = new MultipartFormDataContent();
        form.Add(ImageContent(new byte[] { 1, 2, 3 }, "image/png"), "image", "x.png");

        var response = await client.PostAsync("/api/users/me/avatar", form);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Edit_profile_with_an_empty_display_name_returns_400()
    {
        var client = _factory.CreateClient();
        var user = await RegisterAndLoginAsync(client, "@bad_edit", "Valid Name");

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/users/me")
        {
            Content = JsonContent.Create(new { displayName = "", bio = (string?)null }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Avatar_upload_with_a_disallowed_type_returns_400()
    {
        var client = _factory.CreateClient();
        var user = await RegisterAndLoginAsync(client, "@bad_avatar", "Avatar User");

        using var form = new MultipartFormDataContent();
        form.Add(ImageContent(new byte[] { 1, 2, 3 }, "application/pdf"), "image", "doc.pdf");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/me/avatar") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<UserResponse> UpdateProfileAsync(
        HttpClient client, string accessToken, string displayName, string? bio)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/users/me")
        {
            Content = JsonContent.Create(new { displayName, bio }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        return user!;
    }

    private static async Task<UserResponse> UploadAvatarAsync(
        HttpClient client, string accessToken, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent();
        form.Add(ImageContent(new byte[] { 1, 2, 3, 4 }, contentType), "image", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/users/me/avatar") { Content = form };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        return user!;
    }

    private static async Task<UserResponse> DeleteAvatarAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me/avatar");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(user);
        return user!;
    }

    private static async Task SendActionAsync(HttpClient client, HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<T> GetAsAsync<T>(HttpClient client, string path, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<T>();
        Assert.NotNull(body);
        return body!;
    }

    private static ByteArrayContent ImageContent(byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
    }

    private static async Task<TweetResponse> CreateTweetAsync(
        HttpClient client, string accessToken, string content, Guid? parentId = null)
    {
        using var form = new MultipartFormDataContent { { new StringContent(content), "content" } };
        if (parentId.HasValue)
        {
            form.Add(new StringContent(parentId.Value.ToString()), "parentId");
        }

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

    private record UserResponse(
        Guid Id,
        string Handle,
        string DisplayName,
        string? Bio,
        string? AvatarUrl,
        DateTime CreatedAtUtc,
        int FollowerCount,
        int FollowingCount,
        bool IsFollowedByCurrentUser);

    private record CurrentUserResponse(Guid UserId, string Email, string Handle, string DisplayName, string? AvatarUrl);

    private record PagedTweets(List<TweetResponse> Items, string? NextCursor);

    private record TweetResponse(Guid Id, string Content, Guid? ParentId);
}
