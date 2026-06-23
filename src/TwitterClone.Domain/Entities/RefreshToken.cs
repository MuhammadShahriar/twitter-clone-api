using TwitterClone.Domain.Common;

namespace TwitterClone.Domain.Entities;

/// <summary>
/// A refresh token, persisted <b>hashed</b> (the raw value lives only in the client's httpOnly cookie).
/// Tokens form a <see cref="FamilyId"/> lineage: login starts a family, each rotation links a new token
/// to it. Presenting an already-revoked token is treated as theft → the whole family is revoked.
///
/// References its owner by <see cref="UserId"/> value only — no navigation to the Identity user, so the
/// Domain stays free of any auth/Identity dependency (the FK is configured in Infrastructure).
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Groups a rotation lineage so reuse detection can revoke every token at once.</summary>
    public Guid FamilyId { get; set; }

    /// <summary>SHA-256 hash (hex) of the raw token. The raw token is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Hash of the token that superseded this one on rotation (audit/forensics).</summary>
    public string? ReplacedByTokenHash { get; set; }

    public bool IsRevoked => RevokedAtUtc is not null;

    public bool IsExpired(DateTime utcNow) => ExpiresAtUtc <= utcNow;

    public bool IsActive(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);

    public void Revoke(DateTime utcNow, string? replacedByTokenHash = null)
    {
        RevokedAtUtc ??= utcNow;
        if (replacedByTokenHash is not null)
        {
            ReplacedByTokenHash = replacedByTokenHash;
        }
    }
}
