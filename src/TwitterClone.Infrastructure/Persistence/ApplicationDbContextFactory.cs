using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TwitterClone.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by `dotnet ef` (migrations / scaffolding) so the tooling
/// can construct the context without booting the full API host. Resolves the connection
/// string through the same <see cref="ConnectionStringResolver"/> the runtime uses, so
/// URI parsing and SSL handling are identical in both paths.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // No IConfiguration at design time: take DATABASE_URL if present, else a local default,
        // then run it through the shared resolver so SSL/URI handling matches runtime.
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        var raw = !string.IsNullOrWhiteSpace(databaseUrl)
            ? databaseUrl
            : "Host=localhost;Port=5432;Database=twitterclone;Username=postgres;Password=postgres";

        var connectionString = ConnectionStringResolver.Normalize(raw);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
