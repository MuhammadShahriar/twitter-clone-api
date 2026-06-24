namespace TwitterClone.Application.Authentication.Queries.GetCurrentUser;

/// <summary>The authenticated user's profile, returned by <c>GET /api/auth/me</c>.</summary>
public record CurrentUserDto(Guid UserId, string Email, string Handle, string DisplayName, string? AvatarUrl);
