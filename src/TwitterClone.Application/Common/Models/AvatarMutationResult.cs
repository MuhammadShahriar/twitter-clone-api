namespace TwitterClone.Application.Common.Models;

/// <summary>
/// The outcome of setting or clearing a user's avatar: the updated <see cref="User"/> and the
/// <see cref="PreviousPublicId"/> that was replaced (null if there was no prior avatar). The caller uses
/// the previous public id to best-effort delete the now-orphaned asset from the image host.
/// </summary>
public sealed record AvatarMutationResult(AuthUser User, string? PreviousPublicId);
