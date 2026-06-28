using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TwitterClone.Api.Common;
using TwitterClone.Application.Authentication;
using TwitterClone.Application.Authentication.Commands.Login;
using TwitterClone.Application.Authentication.Commands.Logout;
using TwitterClone.Application.Authentication.Commands.Refresh;
using TwitterClone.Application.Authentication.Commands.Register;
using TwitterClone.Application.Authentication.Queries.GetCurrentUser;

namespace TwitterClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(ISender mediator, IOptions<AuthCookieSettings> cookieOptions) : ControllerBase
{
    private readonly AuthCookieSettings _cookie = cookieOptions.Value;

    /// <summary>Registers a new account. Returns the created user; obtain a token via login.</summary>
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RegisterResult>> Register(
        [FromBody] RegisterCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Authenticates and, on success, returns a JWT access token in the body and sets the refresh
    /// token in an httpOnly cookie.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType(typeof(AuthenticationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthenticationResult>> Login(
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        var tokens = await mediator.Send(command, cancellationToken);
        return IssueOrReject(tokens);
    }

    /// <summary>
    /// Rotates the refresh token (read from the cookie) and returns a fresh access token. Reusing a
    /// rotated/revoked token revokes the whole family and yields 401. No access token required — the
    /// whole point is to recover when the access token has expired.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthenticationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticationResult>> Refresh(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[_cookie.Name];
        var tokens = await mediator.Send(new RefreshCommand(refreshToken), cancellationToken);
        return IssueOrReject(tokens);
    }

    /// <summary>Revokes the refresh token's family and clears the cookie. Always succeeds.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[_cookie.Name];
        await mediator.Send(new LogoutCommand(refreshToken), cancellationToken);
        ClearRefreshCookie();
        return NoContent();
    }

    /// <summary>Returns the currently authenticated user. Requires a valid bearer token.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken cancellationToken)
    {
        var me = await mediator.Send(new GetCurrentUserQuery(), cancellationToken);
        return me is null ? NotFound() : Ok(me);
    }

    // Shared by login and refresh: set the rotated cookie on success, clear it and 401 on failure.
    private ActionResult<AuthenticationResult> IssueOrReject(AuthTokens? tokens)
    {
        if (tokens is null)
        {
            ClearRefreshCookie();
            return Unauthorized();
        }

        SetRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc);
        return Ok(tokens.Result);
    }

    private void SetRefreshCookie(string token, DateTime expiresAtUtc) =>
        Response.Cookies.Append(_cookie.Name, token, BuildCookieOptions(expiresAtUtc));

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(_cookie.Name, BuildCookieOptions(expiresAtUtc: null));

    private CookieOptions BuildCookieOptions(DateTime? expiresAtUtc)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = _cookie.Secure,
            SameSite = Enum.TryParse<SameSiteMode>(_cookie.SameSite, ignoreCase: true, out var mode)
                ? mode
                : SameSiteMode.Lax,
            Path = _cookie.Path,
            IsEssential = true,
        };

        if (!string.IsNullOrWhiteSpace(_cookie.Domain))
        {
            options.Domain = _cookie.Domain;
        }

        if (expiresAtUtc is not null)
        {
            options.Expires = expiresAtUtc;
        }

        return options;
    }
}
