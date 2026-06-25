using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Users.Commands;

/// <summary>
/// Best-effort deletion of a replaced/removed avatar asset from the image host. Shared by the set-avatar
/// (replace) and clear-avatar handlers. A cleanup failure must never fail the user's request — orphaning an
/// image is a far smaller problem than a 500 — so any error is swallowed.
/// </summary>
internal static class AvatarCleanup
{
    public static async Task TryDeleteAsync(
        IImageStorageService imageStorage, string publicId, CancellationToken cancellationToken)
    {
        try
        {
            await imageStorage.DeleteAsync(publicId, cancellationToken);
        }
        catch
        {
            // Intentionally best-effort: the avatar has already been updated in the DB; a failed cleanup of
            // the old asset is non-fatal (at worst it lingers on the host).
        }
    }
}
