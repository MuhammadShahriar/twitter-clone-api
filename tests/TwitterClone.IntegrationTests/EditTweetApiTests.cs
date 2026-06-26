using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TwitterClone.Infrastructure.Persistence;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// HTTP-level integration tests for Module 11A (tweet edit). Drives the real API end-to-end
/// (<see cref="TestWebAppFactory"/>, EF Core in-memory provider): the author can edit their tweet's text
/// within the edit window (stamping <c>editedAtUtc</c>), while a non-author gets 403, a past-window edit gets
/// 409, empty content 400, and a missing tweet 404. Editing changes only the text — author/createdAt/counts
/// are untouched and no mention notifications fire. The past-window case backdates <c>CreatedAtUtc</c> in the
/// shared in-memory store. Fresh handles per test.
/// </summary>
public class EditTweetApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public EditTweetApiTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Author_edits_own_tweet_within_window_and_other_readers_see_it()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_e1", "Alice");

        var tweet = await CreateTweetAsync(client, alice.AccessToken, "first draft");
        Assert.Null(tweet.EditedAtUtc);

        var edited = await EditAsync(client, alice.AccessToken, tweet.Id, "second draft");
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var dto = await ReadTweetAsync(edited);
        Assert.Equal("second draft", dto.Content);
        Assert.NotNull(dto.EditedAtUtc);
        Assert.Equal(tweet.CreatedAtUtc, dto.CreatedAtUtc); // createdAt is untouched by an edit

        // A different (anonymous) reader sees the new content and the edited marker.
        var asReader = await GetTweetAsync(client, tweet.Id);
        Assert.Equal("second draft", asReader.Content);
        Assert.NotNull(asReader.EditedAtUtc);
    }

    [Fact]
    public async Task Non_author_cannot_edit_403()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_e2", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob_e2", "Bob");

        var tweet = await CreateTweetAsync(client, alice.AccessToken, "Alice's tweet.");

        var response = await EditAsync(client, bob.AccessToken, tweet.Id, "Bob's hijack.");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Unchanged.
        Assert.Equal("Alice's tweet.", (await GetTweetAsync(client, tweet.Id)).Content);
    }

    [Fact]
    public async Task Editing_past_the_window_is_409()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_e3", "Alice");

        var tweet = await CreateTweetAsync(client, alice.AccessToken, "Posted a while ago.");

        // Backdate the tweet's CreatedAtUtc beyond the 30-minute window in the shared in-memory store.
        BackdateTweet(tweet.Id, TimeSpan.FromHours(1));

        var response = await EditAsync(client, alice.AccessToken, tweet.Id, "Too late to fix typos.");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // Unchanged.
        Assert.Equal("Posted a while ago.", (await GetTweetAsync(client, tweet.Id)).Content);
    }

    [Fact]
    public async Task Empty_content_is_400()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_e4", "Alice");

        var tweet = await CreateTweetAsync(client, alice.AccessToken, "Has text.");

        var response = await EditAsync(client, alice.AccessToken, tweet.Id, "   ");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Editing_a_missing_tweet_is_404()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_e5", "Alice");

        var response = await EditAsync(client, alice.AccessToken, Guid.NewGuid(), "Editing a ghost.");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Edit_does_not_renotify_mentions_or_change_counts_author_or_createdAt()
    {
        var client = _factory.CreateClient();
        var alice = await RegisterAndLoginAsync(client, "@alice_e6", "Alice");
        var bob = await RegisterAndLoginAsync(client, "@bob_e6", "Bob");
        var carol = await RegisterAndLoginAsync(client, "@carol_e6", "Carol");

        var tweet = await CreateTweetAsync(client, alice.AccessToken, "Original with no mentions.");

        // Bob likes it (count becomes 1).
        await LikeAsync(client, bob.AccessToken, tweet.Id);

        // Alice edits, ADDING a mention of Carol. v1 does not re-notify mentions on edit.
        var edited = await EditAsync(client, alice.AccessToken, tweet.Id, "Now mentioning @carol_e6");
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var dto = await ReadTweetAsync(edited);

        // Text + editedAtUtc changed; everything else is identical.
        Assert.Equal("Now mentioning @carol_e6", dto.Content);
        Assert.NotNull(dto.EditedAtUtc);
        Assert.Equal(tweet.AuthorId, dto.AuthorId);
        Assert.Equal(tweet.CreatedAtUtc, dto.CreatedAtUtc);
        Assert.Equal(tweet.ParentId, dto.ParentId);
        Assert.Equal(1, dto.LikeCount); // the like survived the edit

        // Carol was mentioned only via the edit → no Mention notification was created.
        var carolNotifs = await ListNotificationsAsync(client, carol.AccessToken);
        Assert.DoesNotContain(carolNotifs.Items, n => n.Type == "Mention");
    }

    private void BackdateTweet(Guid id, TimeSpan by)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tweet = db.Tweets.Single(t => t.Id == id);
        tweet.CreatedAtUtc = tweet.CreatedAtUtc - by;
        db.SaveChanges();
    }

    private static Task<HttpResponseMessage> EditAsync(
        HttpClient client, string accessToken, Guid id, string content)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/tweets/{id}")
        {
            Content = JsonContent.Create(new { content }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private static async Task<TweetResponse> GetTweetAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/tweets/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadTweetAsync(response);
    }

    private static async Task<TweetResponse> ReadTweetAsync(HttpResponseMessage response)
    {
        var tweet = await response.Content.ReadFromJsonAsync<TweetResponse>();
        Assert.NotNull(tweet);
        return tweet!;
    }

    private static async Task LikeAsync(HttpClient client, string accessToken, Guid id)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/tweets/{id}/like");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<PagedNotifications> ListNotificationsAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/notifications?limit=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedNotifications>();
        Assert.NotNull(page);
        return page!;
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
        return new AuthedUser(auth!.AccessToken, auth.UserId, auth.Handle);
    }

    private record AuthedUser(string AccessToken, Guid UserId, string Handle);

    private record LoginBody(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Handle, string DisplayName);

    private record PagedNotifications(List<NotificationItem> Items, string? NextCursor);

    private record NotificationItem(Guid Id, string Type);

    private record TweetResponse(
        Guid Id,
        string Content,
        Guid AuthorId,
        DateTime CreatedAtUtc,
        Guid? ParentId,
        int LikeCount,
        DateTime? EditedAtUtc);
}
