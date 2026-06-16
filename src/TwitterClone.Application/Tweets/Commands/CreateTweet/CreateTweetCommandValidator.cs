using FluentValidation;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Tweets.Commands.CreateTweet;

public class CreateTweetCommandValidator : AbstractValidator<CreateTweetCommand>
{
    public CreateTweetCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Tweet content is required.")
            .MaximumLength(Tweet.MaxContentLength)
            .WithMessage($"Tweet content must be {Tweet.MaxContentLength} characters or fewer.");

        RuleFor(x => x.AuthorHandle)
            .NotEmpty().WithMessage("Author handle is required.")
            .MaximumLength(Tweet.MaxAuthorHandleLength)
            .WithMessage($"Author handle must be {Tweet.MaxAuthorHandleLength} characters or fewer.");
    }
}
