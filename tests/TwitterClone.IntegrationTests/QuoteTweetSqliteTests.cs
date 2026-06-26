using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Repository-level tests for the quote-tweet projection in <see cref="TweetRepository"/>'s shared
/// <c>Project</c>, against <b>SQLite</b> (relational). They exercise the one-level <c>QuotedTweet</c> preview
/// (a correlated subquery that joins the quoted tweet to its author and projects a nested <c>Media</c>
/// collection) and the <c>QuoteCount</c> correlated count — neither of which the in-memory provider verifies
/// for real SQL translation. A quote-of-a-quote confirms the preview is non-recursive (one level only), and
/// deleting the quoted tweet confirms the preview goes null ("unavailable") while the quote itself survives.
/// </summary>
public class QuoteTweetSqliteTests
{
    private static readonly DateTime Base = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int minutes) => Base.AddMinutes(minutes);

    [Fact]
    public async Task Quote_projects_a_one_level_preview_with_author_media_and_quote_count()
    {
        using var db = new SqliteTestHarness();

        var alice = Guid.NewGuid();   // author of the quoted original
        var bob = Guid.NewGuid();     // author of the quote
        var viewer = Guid.NewGuid();
        var original = Guid.NewGuid();
        var quote = Guid.NewGuid();
        const string aliceAvatar = "https://images.test/alice.png";

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(alice, "@alice_q", aliceAvatar),
                SqliteTestHarness.NewUser(bob, "@bob_q"),
                SqliteTestHarness.NewUser(viewer, "@viewer_q"));

            var originalTweet = new Tweet("the original", alice) { Id = original, CreatedAtUtc = At(10) };
            originalTweet.AddMedia("https://img/q1.png", "media/q1");
            seed.Tweets.Add(originalTweet);

            // Bob quotes Alice's original (top-level, QuotedTweetId set; ParentId null).
            seed.Tweets.Add(new Tweet("great point!", bob, parentId: null, quotedTweetId: original)
            {
                Id = quote,
                CreatedAtUtc = At(20),
            });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        // The quote carries a one-level preview of the original: id, content, author (with avatar), media.
        var quoteDto = await repository.GetByIdWithAuthorAsync(quote, viewer);
        Assert.NotNull(quoteDto);
        Assert.True(quoteDto!.IsQuote); // created as a quote → flag set
        Assert.NotNull(quoteDto.QuotedTweet);
        Assert.Equal(original, quoteDto.QuotedTweet!.Id);
        Assert.Equal("the original", quoteDto.QuotedTweet.Content);
        Assert.Equal("@alice_q", quoteDto.QuotedTweet.Author.Handle);
        Assert.Equal(aliceAvatar, quoteDto.QuotedTweet.Author.AvatarUrl);
        Assert.Equal(new[] { "media/q1" }, quoteDto.QuotedTweet.Media.Select(m => m.PublicId).ToArray());

        // A non-quote (the original) has a null preview AND is not flagged a quote; and it is quoted once.
        // The IsQuote=false here vs IsQuote=true on a deleted-target quote (below) is what lets the client
        // tell "render nothing" from "render unavailable" — both have a null QuotedTweet.
        var originalDto = await repository.GetByIdWithAuthorAsync(original, viewer);
        Assert.NotNull(originalDto);
        Assert.False(originalDto!.IsQuote);
        Assert.Null(originalDto.QuotedTweet);
        Assert.Equal(1, originalDto.QuoteCount);

        // The quote itself isn't quoted by anyone.
        Assert.Equal(0, quoteDto.QuoteCount);
    }

    [Fact]
    public async Task Quote_of_a_quote_previews_only_one_level()
    {
        using var db = new SqliteTestHarness();

        var a = Guid.NewGuid();
        var original = Guid.NewGuid();
        var inner = Guid.NewGuid();   // quotes original
        var outer = Guid.NewGuid();   // quotes inner

        await using (var seed = db.NewContext())
        {
            seed.Users.Add(SqliteTestHarness.NewUser(a, "@chain"));
            seed.Tweets.AddRange(
                new Tweet("original", a) { Id = original, CreatedAtUtc = At(10) },
                new Tweet("inner quote", a, parentId: null, quotedTweetId: original) { Id = inner, CreatedAtUtc = At(20) },
                new Tweet("outer quote", a, parentId: null, quotedTweetId: inner) { Id = outer, CreatedAtUtc = At(30) });
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        // The outer quote previews the inner quote — and that preview, by construction (QuotedTweetDto has no
        // nested quote field), exposes only one level. No recursion, no deep nesting.
        var outerDto = await repository.GetByIdWithAuthorAsync(outer, currentUserId: null);
        Assert.NotNull(outerDto!.QuotedTweet);
        Assert.Equal(inner, outerDto.QuotedTweet!.Id);
        Assert.Equal("inner quote", outerDto.QuotedTweet.Content);
    }

    [Fact]
    public async Task Deleting_the_quoted_tweet_nulls_the_preview_and_the_quote_survives()
    {
        using var db = new SqliteTestHarness();

        var a = Guid.NewGuid();
        var original = Guid.NewGuid();
        var quote = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.Add(SqliteTestHarness.NewUser(a, "@del"));
            seed.Tweets.AddRange(
                new Tweet("doomed original", a) { Id = original, CreatedAtUtc = At(10) },
                new Tweet("my hot take", a, parentId: null, quotedTweetId: original) { Id = quote, CreatedAtUtc = At(20) });
            await seed.SaveChangesAsync();
        }

        // Delete the quoted original. With ON DELETE SET NULL the quote's QuotedTweetId is nulled; even if the
        // reference dangled, the projection's join would simply miss — either way the preview goes null.
        await using (var del = db.NewContext())
        {
            var originalTweet = await del.Tweets.FindAsync(original);
            del.Tweets.Remove(originalTweet!);
            await del.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        // The quote still exists; its preview is now null, but IsQuote survives the deletion (it isn't the
        // SET-NULL'd FK) — so the client knows to render "This post is unavailable" rather than nothing.
        var quoteDto = await repository.GetByIdWithAuthorAsync(quote, currentUserId: null);
        Assert.NotNull(quoteDto);
        Assert.Equal("my hot take", quoteDto!.Content);
        Assert.Null(quoteDto.QuotedTweet);
        Assert.True(quoteDto.IsQuote);
    }
}
