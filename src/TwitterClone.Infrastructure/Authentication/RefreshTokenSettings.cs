namespace TwitterClone.Infrastructure.Authentication;

/// <summary>Refresh-token options bound from the <c>RefreshToken</c> configuration section.</summary>
public class RefreshTokenSettings
{
    public const string SectionName = "RefreshToken";

    /// <summary>How long a refresh token (and thus a sliding session) remains valid.</summary>
    public int ExpiryDays { get; set; } = 7;
}
