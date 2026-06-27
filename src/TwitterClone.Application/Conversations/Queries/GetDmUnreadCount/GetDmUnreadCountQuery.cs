using MediatR;

namespace TwitterClone.Application.Conversations.Queries.GetDmUnreadCount;

/// <summary>The caller's DM badge count: how many of their conversations have unread messages.</summary>
public record GetDmUnreadCountQuery : IRequest<UnreadConversationsDto>;
