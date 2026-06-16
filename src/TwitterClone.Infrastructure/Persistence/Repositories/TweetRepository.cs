using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Infrastructure.Persistence.Repositories;

/// <summary>
/// Tweet-specific repository. Inherits the generic operations and adds the
/// newest-first listing, with ordering pushed down to the database.
/// </summary>
public class TweetRepository(ApplicationDbContext context)
    : Repository<Tweet>(context), ITweetRepository
{
    public async Task<IReadOnlyList<Tweet>> GetAllNewestFirstAsync(CancellationToken ct = default) =>
        await Context.Tweets
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);
}
