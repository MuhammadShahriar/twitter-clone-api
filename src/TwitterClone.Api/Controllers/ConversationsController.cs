using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TwitterClone.Api.Models;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Conversations;
using TwitterClone.Application.Conversations.Commands.MarkConversationRead;
using TwitterClone.Application.Conversations.Commands.SendMessage;
using TwitterClone.Application.Conversations.Commands.StartConversation;
using TwitterClone.Application.Conversations.Queries.GetConversations;
using TwitterClone.Application.Conversations.Queries.GetDmUnreadCount;
using TwitterClone.Application.Conversations.Queries.GetMessages;

namespace TwitterClone.Api.Controllers;

/// <summary>
/// Direct messages (Module 12A): 1-on-1 conversations, text messages, and per-participant read state. Every
/// action requires authentication and is participant-scoped — a caller can only touch conversations they
/// belong to (otherwise 403; a missing conversation is 404). The actor always comes from the token.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConversationsController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Get-or-create the 1-on-1 conversation between the caller and a recipient (by handle or id). Idempotent:
    /// returns the existing conversation if there is one. Can't message yourself (400); unknown recipient (404).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ConversationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDto>> Start(
        [FromBody] StartConversationRequest request,
        CancellationToken cancellationToken)
    {
        var dto = await mediator.Send(
            new StartConversationCommand(request.RecipientHandle, request.RecipientUserId), cancellationToken);
        return Ok(dto);
    }

    /// <summary>
    /// The caller's conversations, most-recent first, cursor-paginated (each with the other participant, a
    /// last-message preview and the caller's unread count). <c>limit</c> defaults to 20 (max 50).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(CursorPage<ConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CursorPage<ConversationDto>>> List(
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var page = await mediator.Send(new GetConversationsQuery(cursor, limit), cancellationToken);
        return Ok(page);
    }

    /// <summary>The caller's DM badge count: how many of their conversations have unread messages.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadConversationsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UnreadConversationsDto>> UnreadCount(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetDmUnreadCountQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// A conversation's messages, newest-first, cursor-paginated. Participant-only (403 otherwise; 404 if the
    /// conversation does not exist).
    /// </summary>
    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType(typeof(CursorPage<MessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CursorPage<MessageDto>>> Messages(
        Guid id,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var page = await mediator.Send(new GetMessagesQuery(id, cursor, limit), cancellationToken);
        return Ok(page);
    }

    /// <summary>
    /// Sends a text message to a conversation. Participant-only (403 otherwise; 404 if the conversation does
    /// not exist); empty/too-long content is 400. Bumps the conversation's recency.
    /// </summary>
    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType(typeof(MessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageDto>> Send(
        Guid id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var dto = await mediator.Send(new SendMessageCommand(id, request.Content ?? string.Empty), cancellationToken);
        return CreatedAtAction(nameof(Messages), new { id }, dto);
    }

    /// <summary>
    /// Marks the conversation read for the caller (sets their last-read time to now). Participant-only;
    /// returns the now-zero unread count for this conversation.
    /// </summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(typeof(UnreadConversationsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UnreadConversationsDto>> Read(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new MarkConversationReadCommand(id), cancellationToken);
        return Ok(result);
    }
}
