namespace TwitterClone.Application.Common.Models;

/// <summary>
/// A freshly minted refresh token: the <see cref="RawToken"/> (handed to the client, never persisted),
/// its <see cref="TokenHash"/> (what we store), and the absolute expiry derived from configuration.
/// </summary>
public record GeneratedRefreshToken(string RawToken, string TokenHash, DateTime ExpiresAtUtc);
