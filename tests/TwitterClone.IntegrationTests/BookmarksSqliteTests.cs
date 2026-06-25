using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Repository-level tests for the bookmarks read paths against <b>SQLite</b> (relational). They exercise
/// <see cref="TweetRepository.GetBookmarkedTweetsAsync"/> (keyset over the bookmark row's
/// (CreatedAtUtc, TweetId) — a derived ordering the tweet projection cannot carry, mirroring the likes
/// timeline) and the shared <c>Project</c>'s new <c>BookmarkedByCurrentUser</c> flag (a correlated
/// subquery), none of which the in-memory provider translates to SQL. Per the testing policy in CLAUDE.md.
/// </summary>
public class BookmarksSqliteTests
{
    private static readonly DateTime Base = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int minutes) => Base.AddMinutes(minutes);

    [Fact]
    public async Task Bookmarks_translate_ordered_by_bookmark_time_with_flag_and_paginate()
    {
        using var db = new SqliteTestHarness();

        var author = Guid.NewGuid();
        var bookmarker = Guid.NewGuid();

        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();
        var t3 = Guid.NewGuid();
        var notBookmarked = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(author, "@bm_author"),
                SqliteTestHarness.NewUser(bookmarker, "@bm_viewer"));

            seed.Tweets.AddRange(
                new Tweet("t1", author) { Id = t1, CreatedAtUtc = At(10) },
                new Tweet("t2", author) { Id = t2, CreatedAtUtc = At(20) },
                new Tweet("t3", author) { Id = t3, CreatedAtUtc = At(30) },
                new Tweet("not bookmarked", author) { Id = notBookmarked, CreatedAtUtc = At(40) });

            // Bookmarked in an order DIFFERENT from the tweets' own times, so the result must follow the
            // bookmark time (newest bookmark first): t2 (saved last), then t1, then t3.
            seed.Bookmarks.AddRange(
                new Bookmark(bookmarker, t3) { CreatedAtUtc = At(100) },
                new Bookmark(bookmarker, t1) { CreatedAtUtc = At(110) },
                new Bookmark(bookmarker, t2) { CreatedAtUtc = At(120) });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        var page = await repository.GetBookmarkedTweetsAsync(bookmarker, cursor: null, limit: 50);

        // Most-recently-bookmarked first; the never-bookmarked tweet is absent.
        Assert.Equal(new[] { t2, t1, t3 }, page.Items.Select(t => t.Id).ToArray());
        Assert.DoesNotContain(page.Items, t => t.Id == notBookmarked);

        // The owner reads their own bookmarks, so every item carries bookmarkedByCurrentUser = true.
        Assert.All(page.Items, t => Assert.True(t.BookmarkedByCurrentUser));

        // Keyset pagination over the bookmark-time cursor matches the single-shot order, no duplicates/skips.
        var canonical = page.Items.Select(t => t.Id).ToList();
        var paged = new List<Guid>();
        string? cursor = null;
        for (var guard = 0; guard < 100; guard++)
        {
            var pageN = await repository.GetBookmarkedTweetsAsync(bookmarker, cursor, limit: 2);
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
    public async Task BookmarkedByCurrentUser_flag_is_per_caller_and_private()
    {
        using var db = new SqliteTestHarness();

        var author = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var tweet = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(author, "@bm_flag_author"),
                SqliteTestHarness.NewUser(owner, "@bm_flag_owner"),
                SqliteTestHarness.NewUser(other, "@bm_flag_other"));

            seed.Tweets.Add(new Tweet("a tweet", author) { Id = tweet, CreatedAtUtc = At(10) });
            seed.Bookmarks.Add(new Bookmark(owner, tweet));

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        // The owner sees the flag true; another user (bookmarks are private) and an anonymous reader see false.
        Assert.True((await repository.GetByIdWithAuthorAsync(tweet, owner))!.BookmarkedByCurrentUser);
        Assert.False((await repository.GetByIdWithAuthorAsync(tweet, other))!.BookmarkedByCurrentUser);
        Assert.False((await repository.GetByIdWithAuthorAsync(tweet, currentUserId: null))!.BookmarkedByCurrentUser);

        // The other user has bookmarked nothing, so their bookmarks page is empty.
        Assert.Empty((await repository.GetBookmarkedTweetsAsync(other, cursor: null, limit: 50)).Items);
    }
}
