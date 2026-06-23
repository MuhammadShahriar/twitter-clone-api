using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Infrastructure.Persistence.Repositories;

/// <summary>
/// Follow-graph repository. Its lookup is <b>tracked</b> (no <c>AsNoTracking</c>) because the caller stages
/// the returned edge for removal on unfollow and commits via the unit of work. Staging only — no
/// <c>SaveChanges</c> here.
/// </summary>
public class FollowRepository(ApplicationDbContext context)
    : Repository<Follow>(context), IFollowRepository
{
    public async Task<Follow?> FindAsync(Guid followerId, Guid followeeId, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FolloweeId == followeeId, ct);
}
