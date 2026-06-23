using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Infrastructure.Persistence.Repositories;

/// <summary>
/// Like repository. Its lookup is <b>tracked</b> (no <c>AsNoTracking</c>) because the caller stages the
/// returned like for removal on unlike and commits via the unit of work. Staging only — no
/// <c>SaveChanges</c> here.
/// </summary>
public class LikeRepository(ApplicationDbContext context)
    : Repository<Like>(context), ILikeRepository
{
    public async Task<Like?> FindAsync(Guid userId, Guid tweetId, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(l => l.UserId == userId && l.TweetId == tweetId, ct);
}
