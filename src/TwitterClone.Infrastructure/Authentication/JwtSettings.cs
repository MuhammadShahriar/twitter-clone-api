namespace TwitterClone.Infrastructure.Authentication;

/// <summary>
/// JWT signing options bound from the <c>Jwt</c> configuration section. <see cref="SecretKey"/> is a
/// secret and is NOT committed: locally it comes from a user-secret, on Render from the
/// <c>Jwt__SecretKey</c> environment variable. Issuer/Audience/ExpiryMinutes live in appsettings.json.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}
