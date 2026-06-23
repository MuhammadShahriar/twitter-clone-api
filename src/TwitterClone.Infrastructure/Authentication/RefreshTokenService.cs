using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;

namespace TwitterClone.Infrastructure.Authentication;

/// <summary>
/// Generates and hashes refresh tokens. The raw token is 256 bits of cryptographic randomness (hex);
/// only its SHA-256 hash is persisted. Because the raw value is already high-entropy, an unsalted hash
/// is sufficient and lets us look tokens up by hash — no per-token salt needed (unlike a password).
/// </summary>
public class RefreshTokenService(IOptions<RefreshTokenSettings> options) : IRefreshTokenService
{
    private readonly RefreshTokenSettings _settings = options.Value;

    public GeneratedRefreshToken Generate()
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAtUtc = DateTime.UtcNow.AddDays(_settings.ExpiryDays);

        return new GeneratedRefreshToken(rawToken, Hash(rawToken), expiresAtUtc);
    }

    public string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
