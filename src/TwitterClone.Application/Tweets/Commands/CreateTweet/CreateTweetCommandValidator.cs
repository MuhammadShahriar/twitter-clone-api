using FluentValidation;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Tweets.Commands.CreateTweet;

public class CreateTweetCommandValidator : AbstractValidator<CreateTweetCommand>
{
    public CreateTweetCommandValidator(ITweetRepository tweets)
    {
        // A tweet must have text OR at least one image (Twitter allows image-only tweets), so content is
        // only required when nothing is attached. The length cap always applies when content is present.
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("A tweet must have text or at least one image.")
            .When(x => x.Images is not { Count: > 0 });

        RuleFor(x => x.Content)
            .MaximumLength(Tweet.MaxContentLength)
            .WithMessage($"Tweet content must be {Tweet.MaxContentLength} characters or fewer.");

        // A quote tweet is a comment ON another tweet, so it must carry text even when it embeds (and even if
        // it also has images). The non-existent-quoted-id check is in the handler (a 404, not a 400).
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("A quote tweet must include text.")
            .When(x => x.QuotedTweetId.HasValue);

        // A reply must point at a tweet that exists. Surfaces as a 400 via the validation pipeline.
        When(x => x.ParentId.HasValue, () =>
            RuleFor(x => x.ParentId!.Value)
                .MustAsync((parentId, ct) => tweets.ExistsAsync(parentId, ct))
                .WithName(nameof(CreateTweetCommand.ParentId))
                .WithMessage("The tweet you are replying to does not exist."));

        // Cap the number of attached images — checked before any upload happens, so an over-limit
        // request fails fast as a 400 instead of streaming files to the image host.
        RuleFor(x => x.Images!)
            .Must(images => images.Count <= ImageUploadConstraints.MaxImagesPerTweet)
            .When(x => x.Images is not null)
            .WithMessage($"A tweet can have at most {ImageUploadConstraints.MaxImagesPerTweet} images.");

        // Each image must be non-empty, within the size cap, and of an accepted type.
        RuleForEach(x => x.Images).ChildRules(image =>
        {
            image.RuleFor(i => i.Length)
                .GreaterThan(0).WithMessage("An uploaded image cannot be empty.")
                .LessThanOrEqualTo(ImageUploadConstraints.MaxBytesPerImage)
                .WithMessage($"Each image must be {ImageUploadConstraints.MaxBytesPerImage / (1024 * 1024)} MB or smaller.");

            image.RuleFor(i => i.ContentType)
                .Must(ct => ImageUploadConstraints.AllowedContentTypes.Contains(ct))
                .WithMessage("Images must be JPEG, PNG, WebP, or GIF.");
        });
    }
}
