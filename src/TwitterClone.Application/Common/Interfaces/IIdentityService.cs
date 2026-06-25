using TwitterClone.Application.Common.Models;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Abstraction over user management. Implemented in Infrastructure on top of ASP.NET Core Identity so
/// the Application layer stays Identity-free — handlers depend on this, never on <c>UserManager</c>.
/// </summary>
public interface IIdentityService
{
    /// <summary>Creates a user with a hashed password. Returns the new id, or the failure reasons.</summary>
    Task<CreateUserResult> CreateUserAsync(
        string email,
        string handle,
        string displayName,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an identifier/password pair, where the identifier is the user's email <em>or</em> their
    /// @handle (resolved case-insensitively, leading @ optional). Returns
    /// <see cref="CredentialValidationStatus.Success"/> with the user when valid,
    /// <see cref="CredentialValidationStatus.InvalidCredentials"/> when not (deliberately not distinguishing
    /// "no such user" from "wrong password"), or <see cref="CredentialValidationStatus.LockedOut"/> when the
    /// account is locked after too many failed attempts. Failed attempts count toward lockout.
    /// </summary>
    Task<CredentialValidationResult> ValidateCredentialsAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Looks up a user by id, or <c>null</c> if no such user exists.</summary>
    Task<AuthUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the user's editable profile fields (display name and bio). Returns the updated user, or
    /// <c>null</c> if no such user exists.
    /// </summary>
    Task<AuthUser?> UpdateProfileAsync(
        Guid userId,
        string displayName,
        string? bio,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the user's avatar to the given hosted image URL + storage public id. Returns the updated user
    /// together with the previous public id it replaced (so the caller can clean up the old asset), or
    /// <c>null</c> if no such user exists.
    /// </summary>
    Task<AvatarMutationResult?> UpdateAvatarAsync(
        Guid userId,
        string avatarUrl,
        string avatarPublicId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the user's avatar (URL + public id set to null). Returns the updated user together with the
    /// public id that was cleared (so the caller can delete the old asset), or <c>null</c> if no such user
    /// exists. Idempotent: clearing an already-avatarless user succeeds with a null previous public id.
    /// </summary>
    Task<AvatarMutationResult?> ClearAvatarAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
