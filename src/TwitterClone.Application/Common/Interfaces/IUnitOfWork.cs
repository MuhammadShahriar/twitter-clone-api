namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Owns the transaction boundary. Repositories stage changes against a shared
/// context; calling <see cref="SaveChangesAsync"/> commits them all atomically.
/// This is the point of the Unit of Work: many repository operations, one commit.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
