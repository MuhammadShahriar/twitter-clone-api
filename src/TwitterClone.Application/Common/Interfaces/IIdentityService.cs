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
    /// Validates an email/password pair. Returns <see cref="CredentialValidationStatus.Success"/> with the
    /// user when valid, <see cref="CredentialValidationStatus.InvalidCredentials"/> when not (deliberately
    /// not distinguishing "no such user" from "wrong password"), or
    /// <see cref="CredentialValidationStatus.LockedOut"/> when the account is locked after too many failed
    /// attempts. Failed attempts count toward lockout.
    /// </summary>
    Task<CredentialValidationResult> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Looks up a user by id, or <c>null</c> if no such user exists.</summary>
    Task<AuthUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
