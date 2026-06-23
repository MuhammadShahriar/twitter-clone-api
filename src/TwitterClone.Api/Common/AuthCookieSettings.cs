namespace TwitterClone.Api.Common;

/// <summary>
/// Settings for the refresh-token cookie, bound from the <c>AuthCookie</c> configuration section.
/// Cookie handling is an HTTP concern, so it lives entirely in the API layer. <see cref="SameSite"/>
/// and <see cref="Secure"/> are environment-driven: dev uses <c>Lax</c>/insecure over http; prod uses
/// <c>None</c>/secure for a cross-site SPA (Vercel → Render).
/// </summary>
public class AuthCookieSettings
{
    public const string SectionName = "AuthCookie";

    public string Name { get; set; } = "refresh_token";

    /// <summary>Scope the cookie to the auth endpoints so it isn't sent on every API call.</summary>
    public string Path { get; set; } = "/api/auth";

    /// <summary>"Lax" | "Strict" | "None". "None" requires <see cref="Secure"/> = true.</summary>
    public string SameSite { get; set; } = "None";

    public bool Secure { get; set; } = true;

    /// <summary>Optional cookie domain; empty/null means "current host" (the default in dev).</summary>
    public string? Domain { get; set; }
}
