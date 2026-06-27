using TwitterClone.Domain.Common;

namespace TwitterClone.Domain.Entities;

/// <summary>
/// A 1-on-1 direct-message conversation between two users. Like the other social entities it references its
/// members by plain <see cref="Guid"/> only (through <see cref="ConversationParticipant"/>) and carries no
/// navigations — the Domain stays Identity-free; FKs/indexes live in Infrastructure.
/// <para>
/// <see cref="PairKey"/> is the canonical <c>"{minUserId}:{maxUserId}"</c> of the two members (ids sorted so
/// the key is the same whoever starts it). A <b>unique index</b> on it guarantees exactly one conversation
/// per pair — the DB backstop that makes get-or-create idempotent under concurrency.
/// </para>
/// <para><see cref="LastMessageAtUtc"/> is the sort key for the conversation list (most-recent first); it
/// starts at <see cref="BaseEntity.CreatedAtUtc"/> and is bumped by <see cref="RecordMessageAt"/>.</para>
/// </summary>
public class Conversation : BaseEntity
{
    // Parameterless constructor for EF Core materialization only.
    private Conversation()
    {
    }

    public Conversation(Guid userAId, Guid userBId)
    {
        PairKey = BuildPairKey(userAId, userBId);
        LastMessageAtUtc = CreatedAtUtc;
    }

    /// <summary>Canonical key identifying the unordered pair of members; unique across all conversations.</summary>
    public string PairKey { get; private set; } = string.Empty;

    /// <summary>When the most recent message was sent (or the creation time, until the first message).</summary>
    public DateTime LastMessageAtUtc { get; private set; }

    /// <summary>Bumps the conversation's recency to <paramref name="at"/> when a new message is sent.</summary>
    public void RecordMessageAt(DateTime at) => LastMessageAtUtc = at;

    /// <summary>
    /// Builds the canonical pair key for two users by sorting their ids, so the same pair maps to the same
    /// key regardless of who initiates the conversation.
    /// </summary>
    public static string BuildPairKey(Guid a, Guid b)
    {
        var (min, max) = a.CompareTo(b) <= 0 ? (a, b) : (b, a);
        return $"{min}:{max}";
    }
}
