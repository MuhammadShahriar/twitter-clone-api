using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Repository-level tests for <see cref="UserRepository.GetByHandleAsync"/> against <b>SQLite</b>
/// (relational). It projects a <c>UserDto</c> with correlated follower/following count subqueries and a
/// per-caller "followed by me" flag (<c>currentUserId != null &amp;&amp; Follows.Any(...)</c>) — the same
/// conditional-subquery shape used across the tweet reads — so it gets real-SQL translation coverage too.
/// </summary>
public class UserProfileSqliteTests
{
    [Fact]
    public async Task GetByHandle_translates_with_counts_and_per_caller_followed_flag()
    {
        using var db = new SqliteTestHarness();

        var target = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var f1 = Guid.NewGuid();
        var f2 = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(target, "@target"),
                SqliteTestHarness.NewUser(viewer, "@viewer"),
                SqliteTestHarness.NewUser(f1, "@f1"),
                SqliteTestHarness.NewUser(f2, "@f2"));

            // target is followed by viewer, f1, f2 (followerCount 3); target follows f1 (followingCount 1).
            seed.Follows.AddRange(
                new Follow(viewer, target),
                new Follow(f1, target),
                new Follow(f2, target),
                new Follow(target, f1));

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new UserRepository(context);

        // As the viewer (who follows target): flag true, counts correct.
        var asViewer = await repository.GetByHandleAsync("@target", viewer);
        Assert.NotNull(asViewer);
        Assert.Equal(3, asViewer!.FollowerCount);
        Assert.Equal(1, asViewer.FollowingCount);
        Assert.True(asViewer.IsFollowedByCurrentUser);

        // Anonymous reader: same counts, flag false (the currentUserId == null branch).
        var asAnon = await repository.GetByHandleAsync("@target", currentUserId: null);
        Assert.NotNull(asAnon);
        Assert.Equal(3, asAnon!.FollowerCount);
        Assert.False(asAnon.IsFollowedByCurrentUser);

        // Handle lookup is case-insensitive and @-tolerant (matches on the normalized column): the seeded
        // "@target" resolves whether queried with different casing or without the leading @.
        Assert.NotNull(await repository.GetByHandleAsync("@TARGET", viewer));
        Assert.NotNull(await repository.GetByHandleAsync("Target", viewer));

        // Unknown handle => null.
        Assert.Null(await repository.GetByHandleAsync("@nobody", viewer));
    }
}
