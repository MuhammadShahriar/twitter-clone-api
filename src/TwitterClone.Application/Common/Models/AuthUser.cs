namespace TwitterClone.Application.Common.Models;

/// <summary>
/// A provider-agnostic snapshot of an authenticated user, carried across the Application boundary so
/// handlers and the token generator never touch ASP.NET Core Identity types. Infrastructure maps its
/// <c>ApplicationUser</c> onto this; nothing here depends on Identity.
/// </summary>
public record AuthUser(Guid Id, string Email, string Handle, string DisplayName, string? AvatarUrl = null);
