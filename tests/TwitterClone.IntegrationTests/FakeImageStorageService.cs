using System.Collections.Concurrent;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Test double for <see cref="IImageStorageService"/> so the integration tests exercise the
/// upload→create→read spine without hitting Cloudinary over the network. Returns deterministic,
/// filename-derived values so assertions can pin the stored URL/publicId, and records delete calls so
/// tests can assert old-image cleanup on replace/remove.
/// </summary>
internal sealed class FakeImageStorageService : IImageStorageService
{
    private readonly ConcurrentQueue<string> _deletedPublicIds = new();

    /// <summary>The storage public ids passed to <see cref="DeleteAsync"/>, in call order.</summary>
    public IReadOnlyCollection<string> DeletedPublicIds => _deletedPublicIds.ToArray();

    public Task<ImageUploadResult> UploadAsync(ImageUpload image, CancellationToken ct = default) =>
        Task.FromResult(new ImageUploadResult(
            $"https://images.test/{image.FileName}",
            $"fake/{image.FileName}"));

    public Task DeleteAsync(string publicId, CancellationToken ct = default)
    {
        _deletedPublicIds.Enqueue(publicId);
        return Task.CompletedTask;
    }
}
