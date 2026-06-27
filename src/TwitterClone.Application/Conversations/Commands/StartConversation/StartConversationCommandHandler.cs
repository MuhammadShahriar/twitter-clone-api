using FluentValidation;
using FluentValidation.Results;
using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Conversations.Commands.StartConversation;

public class StartConversationCommandHandler(
    IConversationRepository conversationRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<StartConversationCommand, ConversationDto>
{
    public async Task<ConversationDto> Handle(StartConversationCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var callerId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot start a conversation without an authenticated user.");

        // Resolve the recipient: prefer the explicit id, else the handle. Unknown ⇒ 404.
        Guid recipientId;
        if (request.RecipientUserId is Guid id)
        {
            if (!await userRepository.ExistsAsync(id, cancellationToken))
            {
                throw new NotFoundException("User", id);
            }

            recipientId = id;
        }
        else
        {
            recipientId = await userRepository.GetIdByHandleAsync(request.RecipientHandle!, cancellationToken)
                ?? throw new NotFoundException("User", request.RecipientHandle!);
        }

        // You cannot DM yourself — surfaces as a 400 via the validation pipeline.
        if (recipientId == callerId)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(StartConversationCommand.RecipientUserId), "You cannot message yourself.")]);
        }

        var pairKey = Conversation.BuildPairKey(callerId, recipientId);

        // Idempotent get-or-create. If it already exists, return it. Otherwise create the conversation and both
        // participant rows in one transaction; the unique PairKey index is the DB backstop — if a concurrent
        // start wins the race, swallow the unique violation and re-read the now-existing conversation by key.
        var conversationId = await conversationRepository.GetIdByPairKeyAsync(pairKey, cancellationToken);
        if (conversationId is null)
        {
            try
            {
                var conversation = new Conversation(callerId, recipientId);
                await conversationRepository.AddAsync(conversation, cancellationToken);
                await conversationRepository.AddParticipantAsync(
                    new ConversationParticipant(conversation.Id, callerId), cancellationToken);
                await conversationRepository.AddParticipantAsync(
                    new ConversationParticipant(conversation.Id, recipientId), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                conversationId = conversation.Id;
            }
            catch (UniqueConstraintViolationException)
            {
                // A concurrent start already created the conversation for this pair — re-read it.
                conversationId = await conversationRepository.GetIdByPairKeyAsync(pairKey, cancellationToken);
            }
        }

        var dto = await conversationRepository.GetDtoAsync(conversationId!.Value, callerId, cancellationToken);
        return dto!;
    }
}
