namespace TwitterClone.Api.Common;

/// <summary>
/// Options for the built-in ASP.NET Core rate limiter, bound from the <c>RateLimiting</c> config section.
/// Two limiters: a strict per-IP window on the auth endpoints (login/register), complementing the Identity
/// lockout, and a looser global window on writes keyed by the authenticated user id. Reads and the SignalR
/// hubs are never throttled. Defaults are high enough that normal use never trips them; the integration test
/// host sets <see cref="Enabled"/> = false so the suite is unaffected (the dedicated rate-limit tests turn it
/// back on with small limits).
/// </summary>
public class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>Master switch — when false the limiter middleware is not added (used by the test host).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Allowed login/register attempts per <see cref="WindowSeconds"/>, per client IP.</summary>
    public int AuthPermitLimit { get; set; } = 10;

    /// <summary>Allowed write requests per <see cref="WindowSeconds"/>, per authenticated user (fallback IP).</summary>
    public int WritePermitLimit { get; set; } = 120;

    /// <summary>The fixed window length, in seconds, for both limiters.</summary>
    public int WindowSeconds { get; set; } = 60;
}

/// <summary>Names of the rate-limiting policies referenced by <c>[EnableRateLimiting]</c>.</summary>
public static class RateLimitPolicies
{
    /// <summary>Strict per-IP limiter for the auth endpoints (login + register).</summary>
    public const string Auth = "auth";
}
