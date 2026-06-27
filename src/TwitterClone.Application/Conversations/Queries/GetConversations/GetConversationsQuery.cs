using MediatR;
using TwitterClone.Application.Common.Models;

namespace TwitterClone.Application.Conversations.Queries.GetConversations;

/// <summary>The caller's conversations, most-recent first, cursor-paginated.</summary>
public record GetConversationsQuery(string? Cursor, int? Limit) : IRequest<CursorPage<ConversationDto>>;
