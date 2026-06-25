using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Infrastructure.Persistence.Repositories;

/// <summary>
/// Bookmark repository. Its lookup is <b>tracked</b> (no <c>AsNoTracking</c>) because the caller stages the
/// returned bookmark for removal on un-bookmark and commits via the unit of work. Staging only — no
/// <c>SaveChanges</c> here.
/// </summary>
public class BookmarkRepository(ApplicationDbContext context)
    : Repository<Bookmark>(context), IBookmarkRepository
{
    public async Task<Bookmark?> FindAsync(Guid userId, Guid tweetId, CancellationToken ct = default) =>
        await Set.FirstOrDefaultAsync(b => b.UserId == userId && b.TweetId == tweetId, ct);
}
