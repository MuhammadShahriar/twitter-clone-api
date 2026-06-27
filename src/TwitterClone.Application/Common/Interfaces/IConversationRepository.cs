using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Conversations;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Conversation + participant access for direct messages. Inherits the generic staging operations for
/// <see cref="Conversation"/> and adds participant staging, participant-scoped authorization checks, and the
/// read projections (the Identity join lives in Infrastructure, so the Application only ever sees DTOs).
/// </summary>
public interface IConversationRepository : IRepository<Conversation>
{
    /// <summary>The id of the conversation for the given canonical pair key, or <c>null</c> if none exists.</summary>
    Task<Guid?> GetIdByPairKeyAsync(string pairKey, CancellationToken ct = default);

    /// <summary>Stages a new participant row (its membership is committed by the unit of work).</summary>
    Task AddParticipantAsync(ConversationParticipant participant, CancellationToken ct = default);

    /// <summary>True if a conversation with the given id exists.</summary>
    Task<bool> ExistsAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>True if the given user is a participant of the given conversation (the authorization check).</summary>
    Task<bool> IsParticipantAsync(Guid conversationId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// The user ids of all participants of the conversation — used to fan a real-time message out to the
    /// other participant(s) after it is committed.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetParticipantUserIdsAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// The conversation as a <b>tracked</b> entity (so a send can bump its <c>LastMessageAtUtc</c>), or
    /// <c>null</c> if it doesn't exist.
    /// </summary>
    Task<Conversation?> GetTrackedAsync(Guid conversationId, CancellationToken ct = default);

    /// <summary>
    /// The caller's participant row as a <b>tracked</b> entity (so mark-read can update
    /// <c>LastReadAtUtc</c>), or <c>null</c> if the caller is not a participant.
    /// </summary>
    Task<ConversationParticipant?> GetParticipantTrackedAsync(
        Guid conversationId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// A single conversation projected for <paramref name="currentUserId"/> (other participant + last-message
    /// preview + the caller's unread count), or <c>null</c> if it doesn't exist. Used for create/get responses.
    /// </summary>
    Task<ConversationDto?> GetDtoAsync(Guid conversationId, Guid currentUserId, CancellationToken ct = default);

    /// <summary>
    /// The caller's conversations, most-recent first (keyset over <c>(LastMessageAtUtc, Id)</c>),
    /// cursor-paginated. Each row carries the other participant, last-message preview and unread count — all
    /// computed in one translatable query (no N+1).
    /// </summary>
    Task<CursorPage<ConversationDto>> GetConversationsAsync(
        Guid currentUserId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>
    /// How many of the caller's conversations have at least one unread message (newer than their last-read
    /// time and not sent by them) — the DM badge count.
    /// </summary>
    Task<int> GetUnreadConversationCountAsync(Guid currentUserId, CancellationToken ct = default);
}
