namespace TwitterClone.Application.Users;

/// <summary>
/// Lite public profile of a user, returned by <c>GET /api/users/{handle}</c> and by follow/unfollow.
/// The author fields come from a read-side join to the Identity user table (in Infrastructure), so the
/// Application layer never sees the Identity type. <see cref="FollowerCount"/>/<see cref="FollowingCount"/>
/// are computed on the read side (correlated counts); <see cref="IsFollowedByCurrentUser"/> says whether
/// the caller follows this user (always <c>false</c> for an anonymous reader). <see cref="AvatarUrl"/> is
/// the user's hosted avatar image, or <c>null</c> when they have not set one (the client shows a placeholder).
/// </summary>
public record UserDto(
    Guid Id,
    string Handle,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    DateTime CreatedAtUtc,
    int FollowerCount,
    int FollowingCount,
    bool IsFollowedByCurrentUser);
