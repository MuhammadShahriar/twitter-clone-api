namespace TwitterClone.Application.Users;

/// <summary>
/// A "who to follow" suggestion: a lite user the caller does not yet follow. Projected on the read side
/// (in Infrastructure) so the Identity type never crosses into Application. <see cref="FollowerCount"/> is
/// a correlated count. <see cref="AvatarUrl"/> is the user's hosted avatar image, or <c>null</c> when they
/// have not set one (the client falls back to a placeholder).
/// </summary>
public record UserSuggestionDto(
    Guid Id,
    string Handle,
    string DisplayName,
    string? AvatarUrl,
    string? Bio,
    int FollowerCount);
