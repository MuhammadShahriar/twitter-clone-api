namespace TwitterClone.Infrastructure.Media;

/// <summary>
/// Binds the <c>Cloudinary</c> config section. <see cref="ApiSecret"/> is a secret (user-secret locally,
/// <c>Cloudinary__ApiSecret</c> env var on Render) and must never be committed. All three credential fields
/// are validated at startup (see <c>AddInfrastructure</c>) so a misconfiguration aborts the boot instead of
/// surfacing as a runtime 500 on the first upload.
/// </summary>
public sealed class CloudinarySettings
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Folder uploads are placed in within the Cloudinary account.</summary>
    public string UploadFolder { get; set; } = "twitter-clone";
}
