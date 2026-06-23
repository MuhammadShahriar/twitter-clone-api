using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Api.Common;

/// <summary>
/// Reads the current user from the validated JWT principal on the active <see cref="HttpContext"/>.
/// This is the HTTP/JWT-aware side of <see cref="ICurrentUserService"/>; it lives in the API layer so
/// the Application layer stays free of any HTTP or token concepts. JWT bearer auth is configured with
/// <c>MapInboundClaims = false</c>, so claims keep their original names (<c>sub</c>, <c>handle</c>).
/// </summary>
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public string? Handle => User?.FindFirstValue("handle");

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
