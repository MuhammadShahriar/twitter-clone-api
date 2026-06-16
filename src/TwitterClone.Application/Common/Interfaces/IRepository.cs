using TwitterClone.Domain.Common;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Generic repository over an aggregate/entity. Exposes the common persistence
/// operations every entity needs, without leaking EF Core (no <c>IQueryable</c>,
/// no <c>DbContext</c>) into the Application layer.
/// </summary>
/// <remarks>
/// Repositories never call <c>SaveChanges</c>. They stage changes (Add/Update/Remove)
/// against the shared unit of work; only <see cref="IUnitOfWork"/> commits them.
/// </remarks>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default);

    Task AddAsync(T entity, CancellationToken ct = default);

    void Update(T entity);

    void Remove(T entity);
}
