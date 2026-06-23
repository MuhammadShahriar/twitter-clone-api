using FluentValidation;

namespace TwitterClone.Application.Authentication.Commands.Register;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Handle)
            .NotEmpty().WithMessage("Handle is required.")
            .MaximumLength(RegisterCommand.MaxHandleLength)
            .WithMessage($"Handle must be {RegisterCommand.MaxHandleLength} characters or fewer.")
            .Matches("^@?[A-Za-z0-9_]+$")
            .WithMessage("Handle may contain only letters, digits and underscores (an optional leading @).");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(RegisterCommand.MaxDisplayNameLength)
            .WithMessage($"Display name must be {RegisterCommand.MaxDisplayNameLength} characters or fewer.");

        // Complexity (digit/upper/lower/symbol) is enforced by Identity's password options;
        // this just guarantees a sensible minimum length up front.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(RegisterCommand.MinPasswordLength)
            .WithMessage($"Password must be at least {RegisterCommand.MinPasswordLength} characters.");
    }
}
