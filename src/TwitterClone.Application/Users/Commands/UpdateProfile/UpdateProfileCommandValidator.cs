using FluentValidation;

namespace TwitterClone.Application.Users.Commands.UpdateProfile;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(UpdateProfileCommand.MaxDisplayNameLength)
            .WithMessage($"Display name must be {UpdateProfileCommand.MaxDisplayNameLength} characters or fewer.");

        // Bio is optional; only the length is capped when present.
        RuleFor(x => x.Bio)
            .MaximumLength(UpdateProfileCommand.MaxBioLength)
            .WithMessage($"Bio must be {UpdateProfileCommand.MaxBioLength} characters or fewer.");
    }
}
