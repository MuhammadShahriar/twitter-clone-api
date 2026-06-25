using TwitterClone.Application.Notifications;
using TwitterClone.Domain.Entities;
using TwitterClone.Domain.Enums;
using TwitterClone.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Repository-level tests for <see cref="NotificationRepository"/> against <b>SQLite</b> (in-memory,
/// relational), so the queries are really translated to SQL. They exercise the non-trivial parts the
/// non-relational in-memory provider can't vouch for: the actor join + left-join tweet preview projection,
/// the unread-dedup predicate (including the <c>TweetId IS NULL</c> follow case), the unread count, and the
/// keyset pagination over <c>(CreatedAtUtc, Id)</c>.
/// </summary>
public class NotificationsSqliteTests
{
    private static readonly DateTime Base = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int minutes) => Base.AddMinutes(minutes);

    [Fact]
    public async Task List_translates_with_correct_projection_ordering_and_preview()
    {
        using var db = new SqliteTestHarness();

        var me = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var carol = Guid.NewGuid();

        var shortTweet = Guid.NewGuid();
        var longTweet = Guid.NewGuid();
        var reply = Guid.NewGuid();
        var longContent = new string('x', 150);

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(me, "@me"),
                SqliteTestHarness.NewUser(bob, "@bob", avatarUrl: "https://img/bob.png"),
                SqliteTestHarness.NewUser(carol, "@carol"));

            seed.Tweets.AddRange(
                new Tweet("hello world", me) { Id = shortTweet, CreatedAtUtc = At(1) },
                new Tweet(longContent, me) { Id = longTweet, CreatedAtUtc = At(2) },
                new Tweet("nice tweet!", bob, parentId: longTweet) { Id = reply, CreatedAtUtc = At(3) });

            seed.Notifications.AddRange(
                // Read like by Carol on my short tweet (oldest).
                Read(new Notification(me, carol, NotificationType.Like, shortTweet) { CreatedAtUtc = At(5) }),
                // Unread like by Bob on my short tweet.
                new Notification(me, bob, NotificationType.Like, shortTweet) { CreatedAtUtc = At(10) },
                // Unread follow by Carol (no tweet).
                new Notification(me, carol, NotificationType.Follow, tweetId: null) { CreatedAtUtc = At(20) },
                // Unread reply by Bob — tweetId points at the reply itself (newest).
                new Notification(me, bob, NotificationType.Reply, reply) { CreatedAtUtc = At(30) },
                // A notification for SOMEONE ELSE — must never appear in my list.
                new Notification(bob, carol, NotificationType.Follow, tweetId: null) { CreatedAtUtc = At(40) });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new NotificationRepository(context);

        // The real query against a relational provider — if it didn't translate, this throws.
        var page = await repository.GetForRecipientAsync(me, cursor: null, limit: 50);

        // Newest-first, and the other recipient's notification is excluded.
        Assert.Equal(
            new[] { NotificationType.Reply, NotificationType.Follow, NotificationType.Like, NotificationType.Like },
            page.Items.Select(n => n.Type).ToArray());
        Assert.Null(page.NextCursor);

        // Actor projection (handle/display/avatar) comes from the join.
        var replyNote = page.Items[0];
        Assert.Equal("@bob", replyNote.Actor.Handle);
        Assert.Equal("bob", replyNote.Actor.DisplayName);
        Assert.Equal("https://img/bob.png", replyNote.Actor.AvatarUrl);
        Assert.False(replyNote.IsRead);
        // The reply notification points at the reply, and previews the reply's text.
        Assert.Equal(reply, replyNote.TweetId);
        Assert.Equal("nice tweet!", replyNote.TweetPreview);

        // Follow has no associated tweet -> null tweet id and null preview.
        var followNote = page.Items[1];
        Assert.Equal(NotificationType.Follow, followNote.Type);
        Assert.Null(followNote.TweetId);
        Assert.Null(followNote.TweetPreview);
        Assert.Equal("@carol", followNote.Actor.Handle);
        Assert.Null(followNote.Actor.AvatarUrl);

        // The read like (oldest) is still listed but flagged read.
        Assert.True(page.Items[3].IsRead);
    }

    [Fact]
    public async Task Preview_is_truncated_to_100_characters()
    {
        using var db = new SqliteTestHarness();

        var me = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var bigTweet = Guid.NewGuid();
        var content = new string('a', 200);

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(SqliteTestHarness.NewUser(me, "@me_p"), SqliteTestHarness.NewUser(bob, "@bob_p"));
            seed.Tweets.Add(new Tweet(content, me) { Id = bigTweet, CreatedAtUtc = At(1) });
            seed.Notifications.Add(new Notification(me, bob, NotificationType.Like, bigTweet) { CreatedAtUtc = At(2) });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new NotificationRepository(context);

        var page = await repository.GetForRecipientAsync(me, cursor: null, limit: 50);

        Assert.Equal(new string('a', 100), Assert.Single(page.Items).TweetPreview);
    }

    [Fact]
    public async Task UnreadExists_matches_on_tuple_including_null_tweet_and_excludes_read()
    {
        using var db = new SqliteTestHarness();

        var me = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var carol = Guid.NewGuid();
        var tweet = Guid.NewGuid();
        var otherTweet = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(me, "@me_u"),
                SqliteTestHarness.NewUser(bob, "@bob_u"),
                SqliteTestHarness.NewUser(carol, "@carol_u"));
            seed.Tweets.AddRange(
                new Tweet("t", me) { Id = tweet, CreatedAtUtc = At(1) },
                new Tweet("o", me) { Id = otherTweet, CreatedAtUtc = At(2) });
            seed.Notifications.AddRange(
                // Unread like by Bob on `tweet`.
                new Notification(me, bob, NotificationType.Like, tweet) { CreatedAtUtc = At(10) },
                // Unread follow by Bob (null tweet).
                new Notification(me, bob, NotificationType.Follow, tweetId: null) { CreatedAtUtc = At(11) },
                // READ like by Carol on `tweet`.
                Read(new Notification(me, carol, NotificationType.Like, tweet) { CreatedAtUtc = At(12) }));
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new NotificationRepository(context);

        // Exact unread tuple matches (the like, and the null-tweet follow — the IS NULL case).
        Assert.True(await repository.UnreadExistsAsync(me, bob, NotificationType.Like, tweet));
        Assert.True(await repository.UnreadExistsAsync(me, bob, NotificationType.Follow, tweetId: null));

        // A different tweet, the wrong type, or a read notification are NOT matches.
        Assert.False(await repository.UnreadExistsAsync(me, bob, NotificationType.Like, otherTweet));
        Assert.False(await repository.UnreadExistsAsync(me, bob, NotificationType.Retweet, tweet));
        Assert.False(await repository.UnreadExistsAsync(me, carol, NotificationType.Like, tweet));
    }

    [Fact]
    public async Task UnreadCount_counts_only_this_recipients_unread()
    {
        using var db = new SqliteTestHarness();

        var me = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var tweet = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(SqliteTestHarness.NewUser(me, "@me_c"), SqliteTestHarness.NewUser(bob, "@bob_c"));
            seed.Tweets.Add(new Tweet("t", me) { Id = tweet, CreatedAtUtc = At(1) });
            seed.Notifications.AddRange(
                new Notification(me, bob, NotificationType.Like, tweet) { CreatedAtUtc = At(10) },
                new Notification(me, bob, NotificationType.Retweet, tweet) { CreatedAtUtc = At(11) },
                Read(new Notification(me, bob, NotificationType.Follow, tweetId: null) { CreatedAtUtc = At(12) }),
                // Belongs to Bob, not me.
                new Notification(bob, me, NotificationType.Like, tweet) { CreatedAtUtc = At(13) });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new NotificationRepository(context);

        Assert.Equal(2, await repository.GetUnreadCountAsync(me));
    }

    [Fact]
    public async Task Keyset_pagination_has_no_duplicates_or_skips_across_a_tie()
    {
        using var db = new SqliteTestHarness();

        var me = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var tweet = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(SqliteTestHarness.NewUser(me, "@me_k"), SqliteTestHarness.NewUser(bob, "@bob_k"));
            seed.Tweets.Add(new Tweet("t", me) { Id = tweet, CreatedAtUtc = At(1) });
            seed.Notifications.AddRange(
                new Notification(me, bob, NotificationType.Like, tweet) { CreatedAtUtc = At(10) },
                new Notification(me, bob, NotificationType.Retweet, tweet) { CreatedAtUtc = At(20) },
                // A deliberate tie at At(30), exercising the Id tiebreaker across a page boundary.
                new Notification(me, bob, NotificationType.Reply, tweet) { CreatedAtUtc = At(30) },
                new Notification(me, bob, NotificationType.Follow, tweetId: null) { CreatedAtUtc = At(30) });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new NotificationRepository(context);

        var canonical = (await repository.GetForRecipientAsync(me, cursor: null, limit: 50))
            .Items.Select(n => n.Id).ToList();
        Assert.Equal(4, canonical.Count);

        var paged = new List<Guid>();
        string? cursor = null;
        for (var guard = 0; guard < 100; guard++)
        {
            var page = await repository.GetForRecipientAsync(me, cursor, limit: 2);
            paged.AddRange(page.Items.Select(n => n.Id));
            if (page.NextCursor is null)
            {
                break;
            }

            cursor = page.NextCursor;
        }

        Assert.Equal(canonical, paged);
        Assert.Equal(canonical.Count, paged.Distinct().Count());
    }

    // Helper: a notification flipped to read (MarkRead is the only mutator).
    private static Notification Read(Notification n)
    {
        n.MarkRead();
        return n;
    }
}
