namespace TwitterClone.Application.Authentication;

/// <summary>Read model returned by login: the access token, its expiry, and the user it identifies.</summary>
public record AuthenticationResult(
    string AccessToken,
    DateTime ExpiresAtUtc,
    Guid UserId,
    string Handle,
    string DisplayName);
