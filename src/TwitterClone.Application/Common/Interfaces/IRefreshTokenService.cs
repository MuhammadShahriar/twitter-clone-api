using TwitterClone.Application.Common.Models;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Mints and hashes refresh tokens. Implemented in Infrastructure so the cryptography (secure random
/// generation, SHA-256 hashing) and the configured lifetime stay out of the Application layer.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Creates a new high-entropy refresh token (raw + hash + expiry).</summary>
    GeneratedRefreshToken Generate();

    /// <summary>Hashes a raw token the same way <see cref="Generate"/> does, for DB lookups.</summary>
    string Hash(string rawToken);
}
