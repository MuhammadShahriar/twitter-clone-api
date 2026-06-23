using TwitterClone.Domain.Common;

namespace TwitterClone.Domain.Entities;

/// <summary>
/// An image attached to a <see cref="Tweet"/>. Part of the Tweet aggregate — created only via
/// <see cref="Tweet.AddMedia"/> (hence the <c>internal</c> constructor), never on its own. Stores the
/// hosted <see cref="Url"/> and the storage <see cref="PublicId"/> (so the asset can be addressed/removed
/// later); the actual upload to the image host is an Infrastructure concern behind an Application
/// abstraction. <see cref="Position"/> preserves the order the author attached the images in.
/// </summary>
public class TweetMedia : BaseEntity
{
    /// <summary>Max length of a stored media URL.</summary>
    public const int MaxUrlLength = 2048;

    /// <summary>Max length of a storage provider's public id.</summary>
    public const int MaxPublicIdLength = 255;

    // Parameterless constructor for EF Core materialization only.
    private TweetMedia()
    {
    }

    internal TweetMedia(string url, string publicId, int position)
    {
        Url = url;
        PublicId = publicId;
        Position = position;
    }

    /// <summary>The id of the tweet this image belongs to. Set by EF via the aggregate's FK.</summary>
    public Guid TweetId { get; private set; }

    /// <summary>The publicly reachable URL of the uploaded image.</summary>
    public string Url { get; private set; } = string.Empty;

    /// <summary>The storage provider's identifier for the asset (e.g. Cloudinary public id).</summary>
    public string PublicId { get; private set; } = string.Empty;

    /// <summary>Zero-based position of this image within its tweet (attachment order).</summary>
    public int Position { get; private set; }
}
