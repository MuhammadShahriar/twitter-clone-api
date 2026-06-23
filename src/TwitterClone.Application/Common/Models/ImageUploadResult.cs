namespace TwitterClone.Application.Common.Models;

/// <summary>
/// The outcome of storing an image: its publicly reachable <see cref="Url"/> and the storage provider's
/// <see cref="PublicId"/> (kept so the asset can be addressed or removed later). Identity- and
/// provider-agnostic — the Cloudinary specifics stay in Infrastructure.
/// </summary>
public sealed record ImageUploadResult(string Url, string PublicId);
