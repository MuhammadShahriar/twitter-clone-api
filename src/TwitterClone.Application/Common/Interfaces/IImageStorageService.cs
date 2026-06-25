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

    /// <summary>
    /// Deletes the asset with the given storage public id. Used to clean up a replaced/removed avatar so we
    /// don't orphan images on the host. Callers treat this as best-effort (a failure here must not fail the
    /// surrounding request).
    /// </summary>
    Task DeleteAsync(string publicId, CancellationToken ct = default);
}
