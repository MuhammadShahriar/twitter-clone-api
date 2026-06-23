using TwitterClone.Application.Common.Models;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Issues signed JWT access tokens. The signing key, issuer, audience and lifetime are
/// configuration concerns owned by the Infrastructure implementation.
/// </summary>
public interface IJwtTokenGenerator
{
    AccessToken Generate(AuthUser user);
}
