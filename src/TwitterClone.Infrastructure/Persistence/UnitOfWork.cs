using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Infrastructure.Persistence;

/// <summary>
/// Unit of Work over the shared <see cref="ApplicationDbContext"/>. Repositories
/// stage their changes on the same scoped context instance; this commits them all
/// in a single transaction.
///
/// Persistence-race exceptions are translated here (the single commit point) into provider-agnostic
/// Application exceptions, so handlers can treat idempotent races as success without the Application layer
/// ever referencing EF Core / Npgsql. Any other failure propagates unchanged.
/// </summary>
public class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // A row a write expected to change was already changed/removed by a concurrent write.
            throw new ConcurrencyConflictException(ex.Message, ex);
        }
        catch (DbUpdateException ex) when (DbExceptions.IsUniqueViolation(ex))
        {
            // A concurrent insert of the same row won the race to a unique index.
            throw new UniqueConstraintViolationException(ex.Message, ex);
        }
    }
}
