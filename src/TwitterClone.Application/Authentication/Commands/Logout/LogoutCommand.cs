using MediatR;

namespace TwitterClone.Application.Authentication.Commands.Logout;

/// <summary>
/// Revokes the presented refresh token's family (read from the cookie by the controller). Always
/// succeeds — an unknown/absent token is a no-op — so the controller can unconditionally clear the cookie.
/// </summary>
public record LogoutCommand(string? RefreshToken) : IRequest;
