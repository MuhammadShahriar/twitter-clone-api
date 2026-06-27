using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Conversations.Commands.SendMessage;

public class SendMessageCommandHandler(
    IConversationRepository conversationRepository,
    IMessageRepository messageRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<SendMessageCommand, MessageDto>
{
    public async Task<MessageDto> Handle(SendMessageCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var senderId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot send a message without an authenticated user.");

        // Load the conversation tracked so we can bump its recency. Missing ⇒ 404; not a participant ⇒ 403
        // (the conversation exists but isn't the caller's to post to — and we don't leak its contents).
        var conversation = await conversationRepository.GetTrackedAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Conversation), request.ConversationId);

        if (!await conversationRepository.IsParticipantAsync(request.ConversationId, senderId, cancellationToken))
        {
            throw new ForbiddenAccessException();
        }

        var now = DateTime.UtcNow;
        var message = new Message(request.ConversationId, senderId, request.Content.Trim()) { CreatedAtUtc = now };
        await messageRepository.AddAsync(message, cancellationToken);

        // Bump the conversation so it sorts to the top of both participants' lists.
        conversation.RecordMessageAt(now);
        conversationRepository.Update(conversation);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = await messageRepository.GetDtoAsync(message.Id, senderId, cancellationToken);
        return dto!;
    }
}
