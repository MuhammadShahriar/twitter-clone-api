using MediatR;

namespace TwitterClone.Application.Authentication.Commands.Login;

/// <summary>
/// Authenticates by email + password. On success returns <see cref="AuthTokens"/> (access token in the
/// body + a refresh token for the cookie); returns <c>null</c> when the credentials are invalid
/// (mapped to 401 by the controller).
/// </summary>
public record LoginCommand(string Email, string Password) : IRequest<AuthTokens?>;
