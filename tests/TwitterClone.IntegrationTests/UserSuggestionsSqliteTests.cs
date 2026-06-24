using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TwitterClone.Infrastructure.Identity;
using TwitterClone.Infrastructure.Persistence;
using TwitterClone.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Repository-level tests for <see cref="UserRepository.GetSuggestionsAsync"/> against <b>SQLite</b>
/// (in-memory). Unlike the EF Core in-memory provider (which is non-relational and evaluates LINQ in
/// memory), SQLite is relational, so the query is actually translated to SQL — this is what catches
/// translation failures such as ordering over a projected DTO member that wraps a subquery, which the
/// in-memory provider silently lets pass.
/// </summary>
public class UserSuggestionsSqliteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public UserSuggestionsSqliteTests()
    {
        // A shared in-memory SQLite database lives as long as the connection is open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ApplicationDbContext(_options);
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task GetSuggestionsAsync_translates_and_orders_by_follower_count()
    {
        var me = Guid.NewGuid();
        var alpha = Guid.NewGuid();   // 2 followers
        var beta = Guid.NewGuid();    // 1 follower
        var gamma = Guid.NewGuid();   // 0 followers
        var delta = Guid.NewGuid();   // followed by me -> excluded

        await using (var seed = new ApplicationDbContext(_options))
        {
            seed.Users.AddRange(
                NewUser(me, "@me"),
                NewUser(alpha, "@alpha"),
                NewUser(beta, "@beta"),
                NewUser(gamma, "@gamma"),
                NewUser(delta, "@delta"));

            seed.Follows.AddRange(
                new(beta, alpha), new(gamma, alpha),   // alpha = 2
                new(gamma, beta),                       // beta = 1
                new(alpha, delta), new(beta, delta),    // delta has followers...
                new(me, delta));                        // ...and me already follows delta

            await seed.SaveChangesAsync();
        }

        await using var context = new ApplicationDbContext(_options);
        var repository = new UserRepository(context);

        // The real query, executed against a relational provider. If it didn't translate, this would throw.
        var suggestions = await repository.GetSuggestionsAsync(me, limit: 10);

        // Self (me) and the already-followed (delta) are excluded; the rest are most-followed first.
        Assert.Equal(
            new[] { "@alpha", "@beta", "@gamma" },
            suggestions.Select(u => u.Handle).ToArray());
        Assert.Equal(2, suggestions[0].FollowerCount);
        Assert.Equal(1, suggestions[1].FollowerCount);
        Assert.Equal(0, suggestions[2].FollowerCount);
        Assert.All(suggestions, u => Assert.Null(u.AvatarUrl));
    }

    [Fact]
    public async Task GetSuggestionsAsync_respects_the_limit()
    {
        var me = Guid.NewGuid();

        await using (var seed = new ApplicationDbContext(_options))
        {
            seed.Users.Add(NewUser(me, "@me"));
            for (var i = 0; i < 5; i++)
            {
                seed.Users.Add(NewUser(Guid.NewGuid(), $"@other{i}"));
            }

            await seed.SaveChangesAsync();
        }

        await using var context = new ApplicationDbContext(_options);
        var repository = new UserRepository(context);

        var suggestions = await repository.GetSuggestionsAsync(me, limit: 3);

        Assert.Equal(3, suggestions.Count);
    }

    private static ApplicationUser NewUser(Guid id, string handle) => new()
    {
        Id = id,
        Handle = handle,
        NormalizedHandle = HandleNormalizer.Normalize(handle),
        DisplayName = handle.TrimStart('@'),
        UserName = $"{handle.TrimStart('@')}@example.com",
        Email = $"{handle.TrimStart('@')}@example.com",
    };

    public void Dispose() => _connection.Dispose();
}
