using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Conversations;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Message access for direct messages. Inherits the generic staging operations for <see cref="Message"/>
/// (a send only stages; the unit of work commits) and adds the thread read projection, joined to the sender
/// in Infrastructure so the Application sees only <see cref="MessageDto"/>.
/// </summary>
public interface IMessageRepository : IRepository<Message>
{
    /// <summary>
    /// A conversation's messages, <b>newest-first</b> (keyset over <c>(CreatedAtUtc, Id)</c>), cursor-paginated.
    /// <paramref name="currentUserId"/> sets each message's <c>isMine</c> flag. The caller is assumed to have
    /// been authorized as a participant by the handler.
    /// </summary>
    Task<CursorPage<MessageDto>> GetMessagesAsync(
        Guid conversationId, Guid currentUserId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>
    /// A single message projected to its <see cref="MessageDto"/> (sender join + <c>isMine</c> from
    /// <paramref name="currentUserId"/>), or <c>null</c> if it doesn't exist. Used for the send response.
    /// </summary>
    Task<MessageDto?> GetDtoAsync(Guid messageId, Guid currentUserId, CancellationToken ct = default);
}
