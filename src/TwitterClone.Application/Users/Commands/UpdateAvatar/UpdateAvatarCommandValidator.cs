using FluentValidation;
using TwitterClone.Application.Common.Models;

namespace TwitterClone.Application.Users.Commands.UpdateAvatar;

public class UpdateAvatarCommandValidator : AbstractValidator<UpdateAvatarCommand>
{
    public UpdateAvatarCommandValidator()
    {
        // Reuse the tweet-image limits (size/type) for the avatar — checked before any upload, so an empty,
        // oversized, or wrong-type file fails fast as a 400 instead of streaming to the image host.
        RuleFor(x => x.Image).NotNull().WithMessage("An avatar image is required.");

        RuleFor(x => x.Image.Length)
            .GreaterThan(0).WithMessage("The avatar image cannot be empty.")
            .LessThanOrEqualTo(ImageUploadConstraints.MaxBytesPerImage)
            .WithMessage($"The avatar image must be {ImageUploadConstraints.MaxBytesPerImage / (1024 * 1024)} MB or smaller.")
            .When(x => x.Image is not null);

        RuleFor(x => x.Image.ContentType)
            .Must(ct => ImageUploadConstraints.AllowedContentTypes.Contains(ct))
            .When(x => x.Image is not null)
            .WithMessage("The avatar must be a JPEG, PNG, WebP, or GIF image.");
    }
}
