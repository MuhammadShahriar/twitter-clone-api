using CloudinaryDotNet;
using Microsoft.Extensions.Options;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;

namespace TwitterClone.Infrastructure.Media;

/// <summary>
/// Cloudinary-backed <see cref="IImageStorageService"/>. The Cloudinary client (and the API secret) is
/// confined here in Infrastructure; the Application speaks only in <see cref="ImageUpload"/> /
/// <see cref="ImageUploadResult"/>. Uploads are backend-proxied — the bytes flow client → API → Cloudinary,
/// so the storage credentials never reach the browser. <c>Secure = true</c> yields https asset URLs.
/// </summary>
public sealed class CloudinaryImageStorageService : IImageStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly string _uploadFolder;

    public CloudinaryImageStorageService(IOptions<CloudinarySettings> options)
    {
        var settings = options.Value;
        _cloudinary = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret));
        _cloudinary.Api.Secure = true;
        _uploadFolder = settings.UploadFolder;
    }

    public async Task<ImageUploadResult> UploadAsync(ImageUpload image, CancellationToken ct = default)
    {
        // FileDescription wraps the in-memory bytes; Cloudinary streams them to the account's upload folder.
        using var stream = new MemoryStream(image.Content);
        var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams
        {
            File = new FileDescription(image.FileName, stream),
            Folder = _uploadFolder,
        };

        var result = await _cloudinary.UploadAsync(uploadParams, ct);
        if (result.Error is not null)
        {
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");
        }

        return new ImageUploadResult(result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task DeleteAsync(string publicId, CancellationToken ct = default)
    {
        // Best-effort cleanup of a replaced/removed asset. Callers swallow failures, but surface a real
        // Cloudinary error here so the caller can log it if it chooses to.
        var result = await _cloudinary.DestroyAsync(new CloudinaryDotNet.Actions.DeletionParams(publicId));
        if (result.Error is not null)
        {
            throw new InvalidOperationException($"Cloudinary delete failed: {result.Error.Message}");
        }
    }
}
