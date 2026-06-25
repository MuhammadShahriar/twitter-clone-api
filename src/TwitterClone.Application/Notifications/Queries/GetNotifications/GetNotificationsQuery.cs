using MediatR;
using TwitterClone.Application.Common.Models;

namespace TwitterClone.Application.Notifications.Queries.GetNotifications;

/// <summary>Lists the authenticated caller's notifications, newest-first, cursor-paginated.</summary>
public record GetNotificationsQuery(string? Cursor, int? Limit) : IRequest<CursorPage<NotificationDto>>;
