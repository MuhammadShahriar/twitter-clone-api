using FluentValidation;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Conversations.Commands.SendMessage;

public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
{
    public SendMessageCommandValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("A message cannot be empty.")
            .MaximumLength(Message.MaxContentLength)
            .WithMessage($"A message must be {Message.MaxContentLength} characters or fewer.");
    }
}
