using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Notifications;
using TwitterClone.Application.Notifications.Commands.MarkAllRead;
using TwitterClone.Application.Notifications.Queries.GetNotifications;
using TwitterClone.Application.Notifications.Queries.GetUnreadCount;

namespace TwitterClone.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// The caller's notifications, newest-first, cursor-paginated. Pass the previous page's
    /// <c>nextCursor</c> for the next page; <c>limit</c> defaults to 20 (max 50). Requires auth (401).
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(CursorPage<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CursorPage<NotificationDto>>> List(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var page = await mediator.Send(new GetNotificationsQuery(cursor, limit), cancellationToken);
        return Ok(page);
    }

    /// <summary>The caller's unread-notification count (for the bell badge). Requires auth (401).</summary>
    [HttpGet("unread-count")]
    [Authorize]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetUnreadCountQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Marks all of the caller's notifications read and returns the resulting unread count (0).
    /// Requires auth (401). Per-notification mark-read is deferred.
    /// </summary>
    [HttpPost("read")]
    [Authorize]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UnreadCountDto>> MarkAllRead(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new MarkAllReadCommand(), cancellationToken);
        return Ok(result);
    }
}
