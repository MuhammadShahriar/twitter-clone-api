using TwitterClone.Application.Tweets;
using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Repository-level tests for <see cref="TweetRepository.GetFollowingFeedAsync"/> against <b>SQLite</b>
/// (in-memory, relational), so the query is really translated to SQL. This is the most complex read in the
/// app — a UNION (Concat) of authored tweets + retweets, keyset-paginated over an effective sort time — so
/// it most needs real-SQL coverage that the non-relational in-memory provider can't give.
/// </summary>
public class FollowingFeedSqliteTests
{
    // Distinct effective times so the expected order is fully determined (no ties) for the contents test.
    private static readonly DateTime Base = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int minutes) => Base.AddMinutes(minutes);

    private static (Guid Id, Guid? Retweeter) Key(TweetDto t) => (t.Id, t.RetweetedBy?.UserId);

    [Fact]
    public async Task Following_feed_translates_with_correct_contents_ordering_and_retweetedBy()
    {
        using var db = new SqliteTestHarness();

        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var carol = Guid.NewGuid();
        var dave = Guid.NewGuid();

        // Tweets.
        var tb1 = Guid.NewGuid();   // Bob, authored t10 — also retweeted by Carol at t25
        var td2 = Guid.NewGuid();   // Dave, t12 — never followed, never retweeted => must be absent
        var td1 = Guid.NewGuid();   // Dave, t15 — surfaces only via Bob's retweet at t40
        var tc1 = Guid.NewGuid();   // Carol, authored t20
        var tb2 = Guid.NewGuid();   // Bob, authored t30

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(alice, "@alice"),
                SqliteTestHarness.NewUser(bob, "@bob"),
                SqliteTestHarness.NewUser(carol, "@carol"),
                SqliteTestHarness.NewUser(dave, "@dave"));

            // Alice follows Bob and Carol (not Dave).
            seed.Follows.AddRange(new Follow(alice, bob), new Follow(alice, carol));

            seed.Tweets.AddRange(
                new Tweet("tb1", bob) { Id = tb1, CreatedAtUtc = At(10) },
                new Tweet("td2", dave) { Id = td2, CreatedAtUtc = At(12) },
                new Tweet("td1", dave) { Id = td1, CreatedAtUtc = At(15) },
                new Tweet("tc1", carol) { Id = tc1, CreatedAtUtc = At(20) },
                new Tweet("tb2", bob) { Id = tb2, CreatedAtUtc = At(30) });

            seed.Retweets.AddRange(
                new Retweet(carol, tb1) { Id = Guid.NewGuid(), CreatedAtUtc = At(25) },
                new Retweet(bob, td1) { Id = Guid.NewGuid(), CreatedAtUtc = At(40) });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        // The real query, executed against a relational provider. If it didn't translate, this would throw.
        var page = await repository.GetFollowingFeedAsync(alice, cursor: null, limit: 50);

        // Each tweet appears ONCE, at its most-recent surfacing. Newest-first by effective time:
        //   TD1 (rt Bob @40), TB2 (authored @30), TB1 (rt Carol @25 — beats its own authored @10), TC1 (@20).
        var expected = new List<(Guid, Guid?)>
        {
            (td1, bob),
            (tb2, null),
            (tb1, carol),
            (tc1, null),
        };
        Assert.Equal(expected, page.Items.Select(Key).ToList());
        Assert.Null(page.NextCursor);

        // TB1 is de-duplicated: its authored entry (@10) is collapsed into the later retweet surfacing (@25).
        Assert.Single(page.Items, t => t.Id == tb1);

        // retweetedBy is the followed user who surfaced the tweet; null for authored entries.
        Assert.Equal("@bob", page.Items.Single(t => t.Id == td1).RetweetedBy!.Handle);
        Assert.Equal("@carol", page.Items.Single(t => t.Id == tb1).RetweetedBy!.Handle);
        Assert.Null(page.Items.Single(t => t.Id == tb2).RetweetedBy);

        // A non-followed user's tweet that no followee retweeted never appears...
        Assert.DoesNotContain(page.Items, t => t.Id == td2);
        // ...and Dave's TD1 appears ONLY as a retweet, never as an authored entry.
        Assert.DoesNotContain(page.Items, t => t.Id == td1 && t.RetweetedBy == null);
    }

    [Fact]
    public async Task Following_feed_keyset_pagination_has_no_duplicates_or_skips_even_across_a_tie()
    {
        using var db = new SqliteTestHarness();

        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var carol = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(alice, "@alice_pg"),
                SqliteTestHarness.NewUser(bob, "@bob_pg"),
                SqliteTestHarness.NewUser(carol, "@carol_pg"));

            seed.Follows.AddRange(new Follow(alice, bob), new Follow(alice, carol));

            var tb1 = Guid.NewGuid();
            var tb2 = Guid.NewGuid();
            var tc1 = Guid.NewGuid();
            var tc2 = Guid.NewGuid();

            seed.Tweets.AddRange(
                new Tweet("tb1", bob) { Id = tb1, CreatedAtUtc = At(10) },
                new Tweet("tc1", carol) { Id = tc1, CreatedAtUtc = At(20) },
                // A deliberate TIE: TB2 and TC2 share an effective time, exercising the EntryId tiebreaker
                // in the keyset predicate across a page boundary.
                new Tweet("tb2", bob) { Id = tb2, CreatedAtUtc = At(30) },
                new Tweet("tc2", carol) { Id = tc2, CreatedAtUtc = At(30) });

            // A couple of retweets to mix entry kinds into the timeline.
            seed.Retweets.AddRange(
                new Retweet(carol, tb1) { Id = Guid.NewGuid(), CreatedAtUtc = At(25) },
                new Retweet(bob, tc1) { Id = Guid.NewGuid(), CreatedAtUtc = At(35) });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        // Canonical full ordering in one shot.
        var canonical = (await repository.GetFollowingFeedAsync(alice, cursor: null, limit: 50))
            .Items.Select(Key).ToList();
        Assert.True(canonical.Count >= 4, "expected the timeline to span more than one page of size 2");

        // Page through with a small page size and reassemble.
        var paged = new List<(Guid, Guid?)>();
        string? cursor = null;
        for (var guard = 0; guard < 100; guard++)
        {
            var page = await repository.GetFollowingFeedAsync(alice, cursor, limit: 2);
            paged.AddRange(page.Items.Select(Key));
            if (page.NextCursor is null)
            {
                break;
            }

            cursor = page.NextCursor;
        }

        // Same order as the single-shot read (no skips, no reordering) and no entry seen twice.
        Assert.Equal(canonical, paged);
        Assert.Equal(canonical.Count, paged.Distinct().Count());
    }

    [Fact]
    public async Task Following_feed_collapses_a_tweet_to_one_entry_at_its_latest_surfacing()
    {
        using var db = new SqliteTestHarness();

        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var carol = Guid.NewGuid();
        var dave = Guid.NewGuid();
        var erin = Guid.NewGuid(); // not followed — only surfaces via retweets

        var x = Guid.NewGuid();   // retweeted by 3 followees -> ONE entry, latest retweeter
        var y = Guid.NewGuid();   // authored by a followee later than a retweet -> original wins (null)
        var z = Guid.NewGuid();   // retweeted by a followee later than its authored time -> retweet wins

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(alice, "@alice_d"),
                SqliteTestHarness.NewUser(bob, "@bob_d"),
                SqliteTestHarness.NewUser(carol, "@carol_d"),
                SqliteTestHarness.NewUser(dave, "@dave_d"),
                SqliteTestHarness.NewUser(erin, "@erin_d"));

            seed.Follows.AddRange(
                new Follow(alice, bob), new Follow(alice, carol), new Follow(alice, dave));

            seed.Tweets.AddRange(
                new Tweet("x", erin) { Id = x, CreatedAtUtc = At(10) },
                new Tweet("y", bob) { Id = y, CreatedAtUtc = At(40) },
                new Tweet("z", bob) { Id = z, CreatedAtUtc = At(5) });

            seed.Retweets.AddRange(
                // X retweeted by all three followees; latest is Carol @30.
                new Retweet(bob, x) { Id = Guid.NewGuid(), CreatedAtUtc = At(20) },
                new Retweet(carol, x) { Id = Guid.NewGuid(), CreatedAtUtc = At(30) },
                new Retweet(dave, x) { Id = Guid.NewGuid(), CreatedAtUtc = At(25) },
                // Y authored @40 by Bob, retweeted earlier @35 by Carol -> the original is the latest.
                new Retweet(carol, y) { Id = Guid.NewGuid(), CreatedAtUtc = At(35) },
                // Z authored @5 by Bob, retweeted later @50 by Carol -> the retweet is the latest.
                new Retweet(carol, z) { Id = Guid.NewGuid(), CreatedAtUtc = At(50) });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        var page = await repository.GetFollowingFeedAsync(alice, cursor: null, limit: 50);

        // Three distinct tweets, each exactly once, newest-first by effective time: Z(@50), Y(@40), X(@30).
        Assert.Equal(new[] { z, y, x }, page.Items.Select(t => t.Id).ToArray());
        Assert.Equal(3, page.Items.Select(t => t.Id).Distinct().Count());

        // X collapsed across three retweeters to one entry, attributed to the latest (Carol).
        var xEntry = Assert.Single(page.Items, t => t.Id == x);
        Assert.Equal("@carol_d", xEntry.RetweetedBy!.Handle);

        // Y: authored surfacing is later than the retweet -> shown as the original (no retweetedBy).
        Assert.Null(page.Items.Single(t => t.Id == y).RetweetedBy);

        // Z: retweet surfacing is later than the authored time -> shown as the retweet (Carol).
        Assert.Equal("@carol_d", page.Items.Single(t => t.Id == z).RetweetedBy!.Handle);
    }

    [Fact]
    public async Task Following_feed_includes_my_own_tweets_interleaved_by_time()
    {
        using var db = new SqliteTestHarness();

        var me = Guid.NewGuid();
        var followed = Guid.NewGuid();
        var stranger = Guid.NewGuid(); // not followed, not me — must be absent

        var f1 = Guid.NewGuid();   // followed, @10
        var mine = Guid.NewGuid();  // MY OWN tweet, @20
        var s1 = Guid.NewGuid();   // stranger, @25 — excluded
        var f2 = Guid.NewGuid();   // followed, @30

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(me, "@me_feed"),
                SqliteTestHarness.NewUser(followed, "@followed_feed"),
                SqliteTestHarness.NewUser(stranger, "@stranger_feed"));

            seed.Follows.Add(new Follow(me, followed)); // I follow `followed`, not `stranger`

            seed.Tweets.AddRange(
                new Tweet("f1", followed) { Id = f1, CreatedAtUtc = At(10) },
                new Tweet("mine", me) { Id = mine, CreatedAtUtc = At(20) },
                new Tweet("s1", stranger) { Id = s1, CreatedAtUtc = At(25) },
                new Tweet("f2", followed) { Id = f2, CreatedAtUtc = At(30) });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        var page = await repository.GetFollowingFeedAsync(me, cursor: null, limit: 50);

        // My own tweet appears, interleaved by time between the followed user's tweets; newest-first.
        Assert.Equal(new[] { f2, mine, f1 }, page.Items.Select(t => t.Id).ToArray());
        Assert.Contains(page.Items, t => t.Id == mine);

        // A non-followed stranger's tweet (that I didn't author and no followee retweeted) is still excluded.
        Assert.DoesNotContain(page.Items, t => t.Id == s1);
    }

    [Fact]
    public async Task Following_feed_is_empty_when_the_user_follows_no_one()
    {
        using var db = new SqliteTestHarness();

        var loner = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(loner, "@loner"),
                SqliteTestHarness.NewUser(someoneElse, "@someone"));
            seed.Tweets.Add(new Tweet("not in anyone's following feed", someoneElse)
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = At(10),
            });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        var page = await repository.GetFollowingFeedAsync(loner, cursor: null, limit: 50);

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }
}
