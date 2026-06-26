using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Identity;
using TwitterClone.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Repository-level tests for search (<see cref="UserRepository.SearchAsync"/> and
/// <see cref="TweetRepository.SearchAsync"/>) against <b>SQLite</b> (relational). They exercise the
/// case-insensitive <c>ToLower().Contains(...)</c> match (which must translate to SQL — the whole point of
/// not using the Npgsql-only <c>EF.Functions.ILike</c>), the DTO projection (correlated counts + caller
/// flags), and the <c>(CreatedAtUtc, Id)</c> keyset with no duplicates/skips. Per CLAUDE.md's testing policy.
/// </summary>
public class SearchSqliteTests
{
    private static readonly DateTime Base = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int minutes) => Base.AddMinutes(minutes);

    [Fact]
    public async Task User_search_matches_handle_or_display_name_case_insensitively_newest_first()
    {
        using var db = new SqliteTestHarness();

        var viewer = Guid.NewGuid();
        var alice = Guid.NewGuid();      // handle contains "ali"
        var malice = Guid.NewGuid();     // handle contains "ali" too (m-ALI-ce)
        var bob = Guid.NewGuid();        // display name "Alistair" matches "ali"
        var carol = Guid.NewGuid();      // no match

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                NewUserAt(viewer, "@srch_viewer", "Viewer", At(0)),
                NewUserAt(alice, "@Alice_srch", "Alice", At(10)),
                NewUserAt(malice, "@mAlice_srch", "Mallory", At(20)),
                NewUserAt(bob, "@bob_srch", "Alistair Bob", At(30)),
                NewUserAt(carol, "@carol_srch", "Carol", At(40)));

            // The viewer follows alice (for the follow-from-results flag).
            seed.Follows.Add(new Follow(viewer, alice) { CreatedAtUtc = At(50) });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new UserRepository(context);

        // "ALI" (upper) must match @Alice_srch, @mAlice_srch, and display "Alistair Bob" — case-insensitively.
        // Carol does not match. Newest-account first => bob(30), malice(20), alice(10).
        var page = await repository.SearchAsync("ALI", viewer, cursor: null, limit: 50);
        Assert.Equal(new[] { bob, malice, alice }, page.Items.Select(u => u.Id).ToArray());
        Assert.DoesNotContain(page.Items, u => u.Id == carol);

        // Follow-from-results flag reflects the viewer (they follow alice only).
        var byId = page.Items.ToDictionary(u => u.Id);
        Assert.True(byId[alice].IsFollowedByCurrentUser);
        Assert.False(byId[bob].IsFollowedByCurrentUser);

        // Anonymous reader: same matches, flags false.
        var anon = await repository.SearchAsync("ali", currentUserId: null, cursor: null, limit: 50);
        Assert.Equal(new[] { bob, malice, alice }, anon.Items.Select(u => u.Id).ToArray());
        Assert.All(anon.Items, u => Assert.False(u.IsFollowedByCurrentUser));

        // Keyset pagination matches the single-shot order, no duplicates/skips.
        var canonical = page.Items.Select(u => u.Id).ToList();
        var paged = new List<Guid>();
        string? cursor = null;
        for (var guard = 0; guard < 100; guard++)
        {
            var pageN = await repository.SearchAsync("ali", viewer, cursor, limit: 2);
            paged.AddRange(pageN.Items.Select(u => u.Id));
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
    public async Task Tweet_search_matches_content_case_insensitively_newest_first_with_flags()
    {
        using var db = new SqliteTestHarness();

        var author = Guid.NewGuid();
        var viewer = Guid.NewGuid();

        var t1 = Guid.NewGuid();   // "I love Dotnet"
        var t2 = Guid.NewGuid();   // "DOTNET is great" (reply — search spans replies)
        var t3 = Guid.NewGuid();   // "no match here"
        var parent = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                NewUserAt(author, "@srch_t_author", "Author", At(0)),
                NewUserAt(viewer, "@srch_t_viewer", "Viewer", At(1)));

            seed.Tweets.AddRange(
                new Tweet("a parent", author) { Id = parent, CreatedAtUtc = At(5) },
                new Tweet("I love Dotnet", author) { Id = t1, CreatedAtUtc = At(10) },
                new Tweet("DOTNET is great", author, parent) { Id = t2, CreatedAtUtc = At(20) },
                new Tweet("no match here", author) { Id = t3, CreatedAtUtc = At(30) });

            // viewer likes t1 (for the by-me flag).
            seed.Likes.Add(new Like(viewer, t1) { CreatedAtUtc = At(40) });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new TweetRepository(context);

        // "dotnet" (lower) matches t1 and the reply t2, case-insensitively; newest-first => t2(20), t1(10).
        var page = await repository.SearchAsync("dotnet", viewer, cursor: null, limit: 50);
        Assert.Equal(new[] { t2, t1 }, page.Items.Select(t => t.Id).ToArray());
        Assert.DoesNotContain(page.Items, t => t.Id == t3);

        var t1Dto = page.Items.Single(t => t.Id == t1);
        Assert.True(t1Dto.LikedByCurrentUser);
        Assert.Equal(1, t1Dto.LikeCount);

        // Keyset pagination matches the single-shot order, no duplicates/skips.
        var canonical = page.Items.Select(t => t.Id).ToList();
        var paged = new List<Guid>();
        string? cursor = null;
        for (var guard = 0; guard < 100; guard++)
        {
            var pageN = await repository.SearchAsync("DOTNET", viewer, cursor, limit: 1);
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

    /// <summary>A seeded user with an explicit display name and creation time (the keyset order key).</summary>
    private static ApplicationUser NewUserAt(Guid id, string handle, string displayName, DateTime createdAt)
    {
        var user = SqliteTestHarness.NewUser(id, handle);
        user.DisplayName = displayName;
        user.CreatedAtUtc = createdAt;
        return user;
    }
}
