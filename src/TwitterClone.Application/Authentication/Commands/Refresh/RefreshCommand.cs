using MediatR;

namespace TwitterClone.Application.Authentication.Commands.Refresh;

/// <summary>
/// Exchanges a refresh token (read from the httpOnly cookie by the controller) for a fresh access
/// token, rotating the refresh token. Returns <c>null</c> when the token is missing, unknown, expired,
/// or reused — the controller maps that to 401 and clears the cookie.
/// </summary>
public record RefreshCommand(string? RefreshToken) : IRequest<AuthTokens?>;
