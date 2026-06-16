using Microsoft.Extensions.Configuration;
using Npgsql;

namespace TwitterClone.Infrastructure.Persistence;

/// <summary>
/// Single source of truth for turning configuration into a usable Npgsql connection string.
/// Supports two shapes so the same build runs locally and on Render:
///   1. A standard Npgsql connection string in ConnectionStrings:DefaultConnection.
///   2. A URI-style DATABASE_URL (e.g. postgres://user:pass@host:port/db) as Render injects.
/// Both the runtime DI registration and the design-time factory go through here, so
/// they can never drift on URI parsing or SSL handling.
/// </summary>
public static class ConnectionStringResolver
{
    /// <summary>Resolves the connection string from configuration (runtime path).</summary>
    public static string Resolve(IConfiguration configuration)
    {
        // Render and many PaaS providers expose a single DATABASE_URL env var.
        var databaseUrl = configuration["DATABASE_URL"]
            ?? Environment.GetEnvironmentVariable("DATABASE_URL");

        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            return Normalize(databaseUrl);
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No database connection configured. Set DATABASE_URL or ConnectionStrings:DefaultConnection.");
        }

        return Normalize(connectionString);
    }

    /// <summary>
    /// Normalises a raw connection value into an Npgsql connection string. A URI-style
    /// value (postgres:// or postgresql://) is converted and forced to SSL; a value that
    /// is already an Npgsql connection string is returned unchanged.
    /// </summary>
    public static string Normalize(string connectionStringOrUri)
    {
        if (connectionStringOrUri.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            connectionStringOrUri.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return FromUri(connectionStringOrUri);
        }

        return connectionStringOrUri;
    }

    private static string FromUri(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            // Render's (and Neon's) managed Postgres requires SSL.
            SslMode = SslMode.Require,
        };

        return builder.ConnectionString;
    }
}
