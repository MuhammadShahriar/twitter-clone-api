using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Test double for <see cref="IImageStorageService"/> so the integration tests exercise the
/// upload→create→read spine without hitting Cloudinary over the network. Returns deterministic,
/// filename-derived values so assertions can pin the stored URL/publicId.
/// </summary>
internal sealed class FakeImageStorageService : IImageStorageService
{
    public Task<ImageUploadResult> UploadAsync(ImageUpload image, CancellationToken ct = default) =>
        Task.FromResult(new ImageUploadResult(
            $"https://images.test/{image.FileName}",
            $"fake/{image.FileName}"));
}
