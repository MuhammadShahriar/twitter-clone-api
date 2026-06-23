namespace TwitterClone.Application.Common.Models;

/// <summary>A signed JWT access token plus its absolute UTC expiry.</summary>
public record AccessToken(string Value, DateTime ExpiresAtUtc);
