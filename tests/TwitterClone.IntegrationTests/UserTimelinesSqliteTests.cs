using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Repository-level tests for the profile timelines (<see cref="TweetRepository.GetUserTweetsAsync"/> and
/// <see cref="TweetRepository.GetUserLikedTweetsAsync"/>) against <b>SQLite</b> (relational). They exercise
/// the shared <c>Project</c> (correlated count subqueries, per-caller flags, the <c>Media</c> collection
/// projection), the <c>Guid.CompareTo</c> keyset predicate, and — for the likes timeline — a derived ordering
/// over the <c>Like</c> row's time that the tweet projection cannot carry, none of which the in-memory
/// provider translates to SQL.
/// </summary>
public class UserTimelinesSqliteTests
{
    private static readonly DateTime Base = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int minutes) => Base.AddMinutes(minutes);

    [Fact]
    public async Task UserTweets_translate_top_level_only_newest_first_with_counts_and_paginate()
    {
        using var db = new SqliteTestHarness();

        var author = Guid.NewGuid();
        var viewer = Guid.NewGuid();

        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();
        var reply = Guid.NewGuid();
        var othersTweet = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(author, "@tl_author"),
                SqliteTestHarness.NewUser(viewer, "@tl_viewer"));

            seed.Tweets.AddRange(
                new Tweet("p1", author) { Id = p1, CreatedAtUtc = At(10) },
                new Tweet("p2", author) { Id = p2, CreatedAtUtc = At(20) },
                new Tweet("p3", author) { Id = p3, CreatedAtUtc = At(30) },
                // A reply authored by the same user — excluded from their (top-level) timeline.
                new Tweet("a reply", author, p3) { Id = reply, CreatedAtUtc = At(35) },
                // Someone else's tweet — must not appear in the author's timeline.
                new Tweet("not mine", viewer) { Id = othersTweet, CreatedAtUtc = At(40) });

            // p2 liked by the viewer (count 1, likedByCurrentUser true for viewer).
            seed.Likes.Add(new Like(viewer, p2));

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        var page = await repository.GetUserTweetsAsync(author, viewer, cursor: null, limit: 50);

        // Only the author's top-level tweets, newest-first; the reply and the other user's tweet are excluded.
        Assert.Equal(new[] { p3, p2, p1 }, page.Items.Select(t => t.Id).ToArray());
        Assert.DoesNotContain(page.Items, t => t.Id == reply);
        Assert.DoesNotContain(page.Items, t => t.Id == othersTweet);

        var p2Dto = page.Items.Single(t => t.Id == p2);
        Assert.Equal(1, p2Dto.LikeCount);
        Assert.True(p2Dto.LikedByCurrentUser);
        Assert.Equal("@tl_author", p2Dto.AuthorHandle);

        // Keyset pagination matches the single-shot order, with no duplicates/skips.
        var canonical = page.Items.Select(t => t.Id).ToList();
        var paged = new List<Guid>();
        string? cursor = null;
        for (var guard = 0; guard < 100; guard++)
        {
            var pageN = await repository.GetUserTweetsAsync(author, viewer, cursor, limit: 2);
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
    public async Task UserLikes_translate_ordered_by_like_time_with_flags_and_paginate()
    {
        using var db = new SqliteTestHarness();

        var author = Guid.NewGuid();
        var liker = Guid.NewGuid();

        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var t3 = Guid.NewGuid();
        var notLiked = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(author, "@like_author"),
                SqliteTestHarness.NewUser(liker, "@like_viewer"));

            seed.Tweets.AddRange(
                new Tweet("t1", author) { Id = t1, CreatedAtUtc = At(10) },
                new Tweet("t2", author) { Id = t2, CreatedAtUtc = At(20) },
                new Tweet("t3", author) { Id = t3, CreatedAtUtc = At(30) },
                new Tweet("not liked", author) { Id = notLiked, CreatedAtUtc = At(40) });

            // The liker likes t1, t2, t3 — but in an order DIFFERENT from the tweets' own times, so the
            // result must follow the like time (newest like first): t2 (liked last), then t1, then t3.
            seed.Likes.AddRange(
                new Like(liker, t3) { CreatedAtUtc = At(100) },
                new Like(liker, t1) { CreatedAtUtc = At(110) },
                new Like(liker, t2) { CreatedAtUtc = At(120) });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        var page = await repository.GetUserLikedTweetsAsync(liker, liker, cursor: null, limit: 50);

        // Most-recently-liked first; the never-liked tweet is absent.
        Assert.Equal(new[] { t2, t1, t3 }, page.Items.Select(t => t.Id).ToArray());
        Assert.DoesNotContain(page.Items, t => t.Id == notLiked);

        // The caller is the liker, so every item carries likedByCurrentUser = true and a like count of 1.
        Assert.All(page.Items, t =>
        {
            Assert.True(t.LikedByCurrentUser);
            Assert.Equal(1, t.LikeCount);
        });

        // An anonymous reader sees the same liked tweets but no by-me flags.
        var anon = await repository.GetUserLikedTweetsAsync(liker, currentUserId: null, cursor: null, limit: 50);
        Assert.Equal(new[] { t2, t1, t3 }, anon.Items.Select(t => t.Id).ToArray());
        Assert.All(anon.Items, t => Assert.False(t.LikedByCurrentUser));

        // Keyset pagination over the like-time cursor matches the single-shot order, no duplicates/skips.
        var canonical = page.Items.Select(t => t.Id).ToList();
        var paged = new List<Guid>();
        string? cursor = null;
        for (var guard = 0; guard < 100; guard++)
        {
            var pageN = await repository.GetUserLikedTweetsAsync(liker, liker, cursor, limit: 2);
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
}
