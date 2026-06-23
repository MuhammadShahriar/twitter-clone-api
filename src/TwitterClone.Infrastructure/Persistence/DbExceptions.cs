using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TwitterClone.Infrastructure.Persistence;

/// <summary>
/// Helpers for classifying EF Core / Npgsql persistence exceptions. Kept in one place so the unique-violation
/// detection used by <c>IdentityService</c> and <c>UnitOfWork</c> can't drift.
/// </summary>
internal static class DbExceptions
{
    /// <summary>
    /// True when the <see cref="DbUpdateException"/> was caused by a Postgres unique-constraint violation
    /// (SQLSTATE 23505) — e.g. a concurrent insert that lost the race to a unique index.
    /// </summary>
    public static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
