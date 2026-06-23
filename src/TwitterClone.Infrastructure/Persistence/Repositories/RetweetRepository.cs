using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Infrastructure.Persistence.Repositories;

/// <summary>
/// Retweet repository. Its lookup is <b>tracked</b> (no <c>AsNoTracking</c>) because the caller stages the
/// returned retweet for removal on unretweet and commits via the unit of work. Staging only — no
/// <c>SaveChanges</c> here.
/// </summary>
public class RetweetRepository(ApplicationDbContext context)
    : Repository<Retweet>(context), IRetweetRepository
{
    public async Task<Retweet?> FindAsync(Guid userId, Guid tweetId, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(r => r.UserId == userId && r.TweetId == tweetId, ct);
}
