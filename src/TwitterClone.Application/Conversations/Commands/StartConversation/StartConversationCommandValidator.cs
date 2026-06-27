using FluentValidation;

namespace TwitterClone.Application.Conversations.Commands.StartConversation;

public class StartConversationCommandValidator : AbstractValidator<StartConversationCommand>
{
    public StartConversationCommandValidator()
    {
        // Exactly one way to identify the recipient must be supplied.
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.RecipientHandle) || x.RecipientUserId.HasValue)
            .WithMessage("A recipient handle or user id is required.");
    }
}
