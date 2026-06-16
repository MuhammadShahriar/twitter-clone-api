using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Infrastructure.Persistence;

/// <summary>
/// Unit of Work over the shared <see cref="ApplicationDbContext"/>. Repositories
/// stage their changes on the same scoped context instance; this commits them all
/// in a single transaction.
/// </summary>
public class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        await context.SaveChangesAsync(ct);
}
