using Microsoft.AspNetCore.Identity;

namespace TwitterClone.Infrastructure.Identity;

/// <summary>
/// The application user. Extends ASP.NET Core Identity's <see cref="IdentityUser{TKey}"/> with a
/// <see cref="Guid"/> key — so <c>AspNetUsers.Id</c> maps to native <c>uuid</c>, consistent with the
/// domain's <c>Tweet.Id</c> — plus the username / email / password-hash / security-stamp machinery and
/// the Twitter-clone profile fields.
///
/// Identity is an INFRASTRUCTURE / persistence concern, so this type lives in the Infrastructure layer
/// and never leaks into Domain or Application; those layers carry no reference to any Identity package.
/// Domain entities (e.g. <c>Tweet</c>) refer to an author by a value (handle/id), not by this type.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public const int MaxHandleLength = 50;
    public const int MaxDisplayNameLength = 100;
    public const int MaxBioLength = 280;
    public const int MaxAvatarUrlLength = 500;
    public const int MaxAvatarPublicIdLength = 255;

    /// <summary>The public @handle as the user typed it (case + any leading @ preserved). Shown in the UI.</summary>
    public string Handle { get; set; } = string.Empty;

    /// <summary>
    /// The canonical form of <see cref="Handle"/> (see <see cref="HandleNormalizer"/>) — upper-cased, no
    /// leading @. This column carries the unique index and is what every handle lookup compares against, so
    /// handles are matched case-insensitively (<c>@Ada</c> == <c>ada</c>). Set whenever <see cref="Handle"/> is.
    /// </summary>
    public string NormalizedHandle { get; set; } = string.Empty;

    /// <summary>The display name shown on the user's profile and tweets.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional short profile bio.</summary>
    public string? Bio { get; set; }

    /// <summary>Optional URL of the user's avatar image (hosted on the image provider). Null ⇒ placeholder.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// The image host's public id for the current avatar (kept alongside <see cref="AvatarUrl"/> so the asset
    /// can be deleted when the avatar is replaced or removed — avoids orphaning images). Null when no avatar.
    /// </summary>
    public string? AvatarPublicId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
