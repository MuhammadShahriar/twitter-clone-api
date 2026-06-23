using TwitterClone.Application.Tweets;
using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Repository-level tests for the tweet read queries (<see cref="TweetRepository.GetFeedAsync"/>,
/// <see cref="TweetRepository.GetRepliesAsync"/>, <see cref="TweetRepository.GetByIdWithAuthorAsync"/>)
/// against <b>SQLite</b> (relational). They exercise the shared <c>Project</c> — correlated reply/like/
/// retweet count subqueries, the per-caller like/retweet flags, and the <c>Media</c> collection projection
/// — plus the <c>Guid.CompareTo</c> keyset predicate, none of which the in-memory provider translates to SQL.
/// </summary>
public class TweetReadsSqliteTests
{
    private static readonly DateTime Base = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int minutes) => Base.AddMinutes(minutes);

    [Fact]
    public async Task Feed_translates_with_counts_flags_excludes_replies_and_paginates()
    {
        using var db = new SqliteTestHarness();

        var author = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var other = Guid.NewGuid();

        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();
        var reply = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(author, "@author"),
                SqliteTestHarness.NewUser(viewer, "@viewer"),
                SqliteTestHarness.NewUser(other, "@other"));

            seed.Tweets.AddRange(
                new Tweet("p1", author) { Id = p1, CreatedAtUtc = At(10) },
                new Tweet("p2", author) { Id = p2, CreatedAtUtc = At(20) },
                new Tweet("p3", author) { Id = p3, CreatedAtUtc = At(30) },
                // A reply to p1 — must be excluded from the (top-level) feed but counted on p1.
                new Tweet("reply to p1", other, p1) { Id = reply, CreatedAtUtc = At(15) });

            // p2 liked by viewer + other (count 2, likedByCurrentUser true for viewer).
            seed.Likes.AddRange(new Like(viewer, p2), new Like(other, p2));
            // p3 retweeted by viewer (count 1, retweetedByCurrentUser true for viewer).
            seed.Retweets.Add(new Retweet(viewer, p3));

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        var page = await repository.GetFeedAsync(viewer, cursor: null, limit: 50);

        // Top-level only, newest-first; the reply is excluded.
        Assert.Equal(new[] { p3, p2, p1 }, page.Items.Select(t => t.Id).ToArray());
        Assert.DoesNotContain(page.Items, t => t.Id == reply);

        var p1Dto = page.Items.Single(t => t.Id == p1);
        Assert.Equal(1, p1Dto.ReplyCount);
        Assert.Equal("@author", p1Dto.AuthorHandle);
        Assert.Null(p1Dto.RetweetedBy); // the main feed never sets retweetedBy

        var p2Dto = page.Items.Single(t => t.Id == p2);
        Assert.Equal(2, p2Dto.LikeCount);
        Assert.True(p2Dto.LikedByCurrentUser);

        var p3Dto = page.Items.Single(t => t.Id == p3);
        Assert.Equal(1, p3Dto.RetweetCount);
        Assert.True(p3Dto.RetweetedByCurrentUser);

        // An anonymous reader gets the same counts but no by-me flags.
        var anon = (await repository.GetFeedAsync(null, cursor: null, limit: 50)).Items.Single(t => t.Id == p2);
        Assert.Equal(2, anon.LikeCount);
        Assert.False(anon.LikedByCurrentUser);

        // Keyset pagination (exercises the Guid.CompareTo predicate): paging matches the single-shot order.
        var canonical = page.Items.Select(t => t.Id).ToList();
        var paged = new List<Guid>();
        string? cursor = null;
        for (var guard = 0; guard < 100; guard++)
        {
            var pageN = await repository.GetFeedAsync(viewer, cursor, limit: 2);
            paged.AddRange(pageN.Items.Select(t => t.Id));
            if (pageN.NextCursor is null)
            {
                break;
            }

            cursor = pageN.NextCursor;
        }

        Assert.Equal(canonical, paged);
        Assert.Equal(canonical.Count, paged.Distinct().Count());
    }

    [Fact]
    public async Task Replies_translate_oldest_first_with_counts()
    {
        using var db = new SqliteTestHarness();

        var author = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var ra = Guid.NewGuid();
        var rb = Guid.NewGuid();
        var rc = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(author, "@thread_author"),
                SqliteTestHarness.NewUser(viewer, "@thread_viewer"));

            seed.Tweets.AddRange(
                new Tweet("parent", author) { Id = parent, CreatedAtUtc = At(0) },
                new Tweet("ra", author, parent) { Id = ra, CreatedAtUtc = At(10) },
                new Tweet("rb", author, parent) { Id = rb, CreatedAtUtc = At(20) },
                new Tweet("rc", author, parent) { Id = rc, CreatedAtUtc = At(30) });

            seed.Likes.Add(new Like(viewer, rb)); // rb liked by viewer

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        var page = await repository.GetRepliesAsync(parent, viewer, cursor: null, limit: 50);

        // Replies read oldest-first (natural thread order).
        Assert.Equal(new[] { ra, rb, rc }, page.Items.Select(t => t.Id).ToArray());
        Assert.All(page.Items, t => Assert.Equal(parent, t.ParentId));

        var rbDto = page.Items.Single(t => t.Id == rb);
        Assert.Equal(1, rbDto.LikeCount);
        Assert.True(rbDto.LikedByCurrentUser);
    }

    [Fact]
    public async Task Detail_translates_with_counts_flags_and_media()
    {
        using var db = new SqliteTestHarness();

        var author = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var tweetId = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(author, "@detail_author"),
                SqliteTestHarness.NewUser(viewer, "@detail_viewer"));

            var tweet = new Tweet("a tweet with media", author) { Id = tweetId, CreatedAtUtc = At(10) };
            tweet.AddMedia("https://img/1.png", "media/1");
            tweet.AddMedia("https://img/2.png", "media/2");
            seed.Tweets.Add(tweet);

            seed.Likes.Add(new Like(viewer, tweetId));

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        var dto = await repository.GetByIdWithAuthorAsync(tweetId, viewer);

        Assert.NotNull(dto);
        Assert.Equal("@detail_author", dto!.AuthorHandle);
        Assert.Equal(1, dto.LikeCount);
        Assert.True(dto.LikedByCurrentUser);
        Assert.Equal(0, dto.ReplyCount);

        // The Media collection projection (an ordered correlated sub-select) round-trips.
        Assert.Equal(new[] { "media/1", "media/2" }, dto.Media.Select(m => m.PublicId).ToArray());
        Assert.Equal(new[] { 0, 1 }, dto.Media.Select(m => m.Position).ToArray());
    }
}
