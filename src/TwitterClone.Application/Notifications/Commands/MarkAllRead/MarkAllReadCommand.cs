using MediatR;

namespace TwitterClone.Application.Notifications.Commands.MarkAllRead;

/// <summary>
/// Marks all of the authenticated caller's notifications read. Returns the resulting unread count (0).
/// Per-notification mark-read is deferred to a later sub-step.
/// </summary>
public record MarkAllReadCommand : IRequest<UnreadCountDto>;
