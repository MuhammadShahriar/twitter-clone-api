namespace TwitterClone.Application.Conversations;

/// <summary>
/// The lightweight user fields shown in DM views — the other participant on a conversation row, and the
/// sender on a message. Populated by a read-side join to the Identity user table (in Infrastructure), so the
/// Application layer never sees the Identity type.
/// </summary>
public record ChatUserDto(Guid Id, string Handle, string DisplayName, string? AvatarUrl);
