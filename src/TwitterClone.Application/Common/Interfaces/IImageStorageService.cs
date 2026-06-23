using TwitterClone.Application.Common.Models;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Stores an image and returns its hosted URL + storage public id. The Application depends only on this
/// abstraction; the concrete image host (Cloudinary) and its secret live in Infrastructure. The upload is
/// proxied through the backend so the storage credentials never reach the client.
/// </summary>
public interface IImageStorageService
{
    /// <summary>Uploads one image and returns its hosted URL and storage public id.</summary>
    Task<ImageUploadResult> UploadAsync(ImageUpload image, CancellationToken ct = default);
}
