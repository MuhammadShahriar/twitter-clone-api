namespace TwitterClone.Application.Common.Models;

/// <summary>The outcome of validating a login's email/password pair.</summary>
public enum CredentialValidationStatus
{
    /// <summary>Credentials are valid; <see cref="CredentialValidationResult.User"/> is populated.</summary>
    Success,

    /// <summary>No such user, or the password was wrong. Deliberately indistinguishable to the caller.</summary>
    InvalidCredentials,

    /// <summary>The account is temporarily locked after too many failed attempts (brute-force defense).</summary>
    LockedOut,
}

/// <summary>
/// Provider-agnostic result of <c>IIdentityService.ValidateCredentialsAsync</c>. Lets the Application
/// distinguish "wrong credentials" (→ 401) from "locked out" (→ 423) without ever seeing Identity's
/// <c>SignInResult</c>.
/// </summary>
public record CredentialValidationResult(CredentialValidationStatus Status, AuthUser? User)
{
    public static CredentialValidationResult Success(AuthUser user) =>
        new(CredentialValidationStatus.Success, user);

    public static readonly CredentialValidationResult Invalid =
        new(CredentialValidationStatus.InvalidCredentials, null);

    public static readonly CredentialValidationResult LockedOut =
        new(CredentialValidationStatus.LockedOut, null);
}
