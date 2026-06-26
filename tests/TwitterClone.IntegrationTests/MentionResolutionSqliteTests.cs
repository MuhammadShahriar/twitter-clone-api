using TwitterClone.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Repository-level test for <see cref="UserRepository.GetIdsByHandlesAsync"/> against <b>SQLite</b>
/// (relational), so the <c>NormalizedHandle</c> matching really translates to SQL (a <c>Contains</c> over a
/// list ⇒ <c>IN (...)</c>). Asserts the batch resolves handles case-insensitively and @-tolerantly, skips
/// unknown handles, and returns distinct ids — the resolution that turns parsed mentions into recipients.
/// </summary>
public class MentionResolutionSqliteTests
{
    [Fact]
    public async Task Resolves_known_handles_case_insensitively_and_skips_unknown()
    {
        using var db = new SqliteTestHarness();

        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(alice, "@alice"),
                SqliteTestHarness.NewUser(bob, "@bob"));
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new UserRepository(context);

        // Mixed casing, with/without '@', a repeat, and an unknown handle. The known two resolve (distinctly);
        // "@nobody" is absent.
        var ids = await repository.GetIdsByHandlesAsync(
            new[] { "ALICE", "@alice", "bob", "@nobody" });

        Assert.Equal(2, ids.Count);
        Assert.Contains(alice, ids);
        Assert.Contains(bob, ids);
    }

    [Fact]
    public async Task Empty_input_returns_empty_without_querying()
    {
        using var db = new SqliteTestHarness();
        await using var context = db.NewContext();
        var repository = new UserRepository(context);

        Assert.Empty(await repository.GetIdsByHandlesAsync(Array.Empty<string>()));
    }
}
