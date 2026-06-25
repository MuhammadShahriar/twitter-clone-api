using MediatR;

namespace TwitterClone.Application.Notifications.Queries.GetUnreadCount;

/// <summary>Returns the authenticated caller's unread-notification count (for the bell badge).</summary>
public record GetUnreadCountQuery : IRequest<UnreadCountDto>;
