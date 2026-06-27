using MediatR;
using TwitterClone.Application.Common.Models;

namespace TwitterClone.Application.Conversations.Queries.GetMessages;

/// <summary>A conversation's messages, newest-first, cursor-paginated. Participant-only (enforced in the handler).</summary>
public record GetMessagesQuery(Guid ConversationId, string? Cursor, int? Limit)
    : IRequest<CursorPage<MessageDto>>;
