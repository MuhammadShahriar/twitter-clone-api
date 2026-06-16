using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Tweet-specific repository. Entity-specific queries live here as named methods
/// (rather than exposing <c>IQueryable</c>), keeping the abstraction clean.
/// </summary>
public interface ITweetRepository : IRepository<Tweet>
{
    /// <summary>Lists all tweets ordered newest-first (by <c>CreatedAtUtc</c> descending).</summary>
    Task<IReadOnlyList<Tweet>> GetAllNewestFirstAsync(CancellationToken ct = default);
}
