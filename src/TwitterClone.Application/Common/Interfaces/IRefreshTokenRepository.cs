using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Refresh-token persistence. Lookups return <b>tracked</b> entities (unlike the read-only generic
/// repository) because callers rotate/revoke them and then commit via the unit of work.
/// </summary>
public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    /// <summary>Finds a token by its stored hash, or <c>null</c>. Tracked for mutation.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Returns every token in a rotation family (tracked), so the family can be revoked together.</summary>
    Task<IReadOnlyList<RefreshToken>> GetByFamilyAsync(Guid familyId, CancellationToken ct = default);
}
