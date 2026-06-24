using MediatR;

namespace TwitterClone.Application.Users.Commands.UpdateProfile;

/// <summary>
/// Updates the authenticated caller's editable profile fields (display name and optional bio) and returns
/// their refreshed lite profile. The user is taken from the token (never the body), so there is no id/handle
/// here. The validator caps the lengths.
/// </summary>
public record UpdateProfileCommand(string DisplayName, string? Bio) : IRequest<UserDto>
{
    // Validation bounds live here (Application-side) so the layer stays free of Infrastructure's
    // ApplicationUser. The EF column lengths in Infrastructure mirror these.
    public const int MaxDisplayNameLength = 100;
    public const int MaxBioLength = 280;
}
