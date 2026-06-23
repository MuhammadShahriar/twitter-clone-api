using MediatR;

namespace TwitterClone.Application.Authentication.Commands.Register;

/// <summary>Registers a new account. The password is hashed by Identity in Infrastructure.</summary>
public record RegisterCommand(string Email, string Handle, string DisplayName, string Password)
    : IRequest<RegisterResult>
{
    // Validation bounds live here (Application-side) so the layer stays free of Infrastructure's
    // ApplicationUser. The EF column lengths in Infrastructure mirror these.
    public const int MaxHandleLength = 50;
    public const int MaxDisplayNameLength = 100;
    public const int MinPasswordLength = 8;
}
