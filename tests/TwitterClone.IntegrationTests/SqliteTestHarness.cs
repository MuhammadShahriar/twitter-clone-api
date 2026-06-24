using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TwitterClone.Infrastructure.Identity;
using TwitterClone.Infrastructure.Persistence;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// A throwaway relational database for repository-level tests, backed by in-memory SQLite. SQLite is a
/// real relational provider, so queries are actually translated to SQL — unlike the EF Core <b>in-memory</b>
/// provider (non-relational), which evaluates LINQ in process and therefore <b>hides SQL-translation
/// failures</b>. Any repository query with subqueries, projections to a DTO, unions/Concat, or keyset
/// pagination must be covered here (see the testing policy in CLAUDE.md), not only via the in-memory provider.
///
/// The in-memory database lives as long as the open connection; dispose the harness to tear it down.
/// </summary>
internal sealed class SqliteTestHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ApplicationDbContext(Options);
        context.Database.EnsureCreated();
    }

    public DbContextOptions<ApplicationDbContext> Options { get; }

    /// <summary>A fresh context over the same in-memory database (seed in one, query in another).</summary>
    public ApplicationDbContext NewContext() => new(Options);

    /// <summary>Builds a minimally-populated Identity user (just enough columns to insert and read back).</summary>
    public static ApplicationUser NewUser(Guid id, string handle) => new()
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
