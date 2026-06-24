using FluentValidation;

namespace TwitterClone.Application.Authentication.Commands.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // The identifier may be an email OR an @handle, so do NOT force email format here — only require
        // that something was supplied. IdentityService decides email-vs-handle and resolves it.
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email or username is required.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
