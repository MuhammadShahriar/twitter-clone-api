namespace TwitterClone.Application.Authentication;

/// <summary>
/// What an auth handler hands back to the controller: the response body (<see cref="Result"/>, which
/// carries the in-memory access token) plus the raw refresh token and its expiry. The controller puts
/// <see cref="Result"/> in the response body and the refresh token in an httpOnly cookie — the raw
/// refresh token is never serialized into the body.
/// </summary>
public record AuthTokens(AuthenticationResult Result, string RefreshToken, DateTime RefreshTokenExpiresAtUtc);
