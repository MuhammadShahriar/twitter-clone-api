namespace TwitterClone.Application.Common.Models;

/// <summary>
/// Limits on images attached to a tweet, enforced by the create-tweet validator (so an over-limit upload
/// fails as a 400 before anything reaches the image host).
/// </summary>
public static class ImageUploadConstraints
{
    /// <summary>Maximum number of images per tweet.</summary>
    public const int MaxImagesPerTweet = 4;

    /// <summary>Maximum size of a single image, in bytes (5 MB).</summary>
    public const long MaxBytesPerImage = 5 * 1024 * 1024;

    /// <summary>Accepted image content types (matched case-insensitively).</summary>
    public static readonly IReadOnlySet<string> AllowedContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif",
        };
}
