namespace TwitterClone.Application.Authentication;

/// <summary>Read model returned by register: the newly created account (no token — login issues that).</summary>
public record RegisterResult(Guid UserId, string Email, string Handle, string DisplayName);
