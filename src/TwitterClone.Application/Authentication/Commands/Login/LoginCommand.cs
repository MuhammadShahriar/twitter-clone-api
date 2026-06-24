using MediatR;

namespace TwitterClone.Application.Authentication.Commands.Login;

/// <summary>
/// Authenticates by identifier + password, where the identifier is the user's email <em>or</em> their
/// @handle (case-insensitive, leading @ optional). On success returns <see cref="AuthTokens"/> (access
/// token in the body + a refresh token for the cookie); returns <c>null</c> when the credentials are
/// invalid (mapped to 401 by the controller).
/// </summary>
/// <remarks>
/// The wire field is named <c>email</c> for backward compatibility with the existing login form (which
/// labels it "Email or username"); the value is treated as an email-or-handle identifier, not strictly
/// an email.
/// </remarks>
public record LoginCommand(string Email, string Password) : IRequest<AuthTokens?>;
