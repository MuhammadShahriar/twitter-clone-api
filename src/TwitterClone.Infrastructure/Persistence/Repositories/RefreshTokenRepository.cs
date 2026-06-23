using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Infrastructure.Persistence.Repositories;

/// <summary>
/// Refresh-token repository. Its lookups are <b>tracked</b> (no <c>AsNoTracking</c>): callers revoke /
/// rotate the returned entities and commit via the unit of work. Staging only — no <c>SaveChanges</c> here.
/// </summary>
public class RefreshTokenRepository(ApplicationDbContext context)
    : Repository<RefreshToken>(context), IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RefreshToken>> GetByFamilyAsync(Guid familyId, CancellationToken ct = default) =>
        await Set.Where(t => t.FamilyId == familyId).ToListAsync(ct);
}
