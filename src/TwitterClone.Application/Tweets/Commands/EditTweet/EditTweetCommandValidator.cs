using FluentValidation;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Tweets.Commands.EditTweet;

/// <summary>
/// Validates an edit: the new text must be non-empty and within the same length cap as create. (An edit is a
/// text operation — v1 doesn't change media — so unlike create's "media-or-text" rule, edited content is
/// always required.) Author-only and the edit-window check live in the handler (403 / 409 respectively).
/// </summary>
public class EditTweetCommandValidator : AbstractValidator<EditTweetCommand>
{
    public EditTweetCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("A tweet must have text.")
            .MaximumLength(Tweet.MaxContentLength)
            .WithMessage($"Tweet content must be {Tweet.MaxContentLength} characters or fewer.");
    }
}
