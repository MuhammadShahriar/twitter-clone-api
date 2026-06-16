using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Common;

namespace TwitterClone.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of the generic repository. Staging methods (Add/Update/Remove)
/// only track changes on the shared <see cref="ApplicationDbContext"/>; they never persist.
/// Committing is the unit of work's job (see <see cref="UnitOfWork"/>).
/// </summary>
public class Repository<T>(ApplicationDbContext context) : IRepository<T>
    where T : BaseEntity
{
    protected readonly ApplicationDbContext Context = context;

    protected DbSet<T> Set => Context.Set<T>();

    // Reads use AsNoTracking — these entities are returned for display, not mutation.
    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await Set.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking().ToListAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await Set.AddAsync(entity, ct);

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity) => Set.Remove(entity);
}
