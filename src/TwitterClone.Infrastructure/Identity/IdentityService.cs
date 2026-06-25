using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Infrastructure.Persistence;

namespace TwitterClone.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity implementation of <see cref="IIdentityService"/>. This is the ONLY place the
/// Application's auth use-cases touch Identity — it maps <see cref="ApplicationUser"/> to/from the
/// Identity-free models the Application layer speaks in.
/// </summary>
public class IdentityService(UserManager<ApplicationUser> userManager) : IIdentityService
{
    // A precomputed password hash used to equalize timing when the email is unknown: we run an
    // equivalent-cost verification so a non-existent account doesn't respond measurably faster than a
    // real one (user-enumeration defense). Computed once; the value itself is irrelevant.
    private static readonly PasswordHasher<ApplicationUser> DummyHasher = new();
    private static readonly ApplicationUser DummyUser = new();
    private static readonly string DummyPasswordHash =
        DummyHasher.HashPassword(DummyUser, "timing-equalizer-not-a-real-password-1!");

    // "Real email shape": an @ that isn't first, with a dot somewhere after it (a domain). Handles can't
    // satisfy this (the register regex forbids dots), and an @-prefixed handle has its @ at index 0 — so
    // this cleanly routes "ada@x.com" to the email lookup and "@ada"/"ada" to the handle lookup.
    private static bool LooksLikeEmail(string value)
    {
        var at = value.IndexOf('@');
        return at > 0 && value.IndexOf('.', at) > at + 1;
    }

    public async Task<CreateUserResult> CreateUserAsync(
        string email,
        string handle,
        string displayName,
        string password,
        CancellationToken cancellationToken = default)
    {
        // Handle uniqueness is enforced only by our unique index (it is not Identity's UserName), so
        // check it up front to return a friendly message instead of a raw DB constraint violation. The
        // check runs on the normalized form, so a casing/@-only variant of an existing handle is rejected.
        var normalizedHandle = HandleNormalizer.Normalize(handle);
        var handleTaken = await userManager.Users
            .AnyAsync(u => u.NormalizedHandle == normalizedHandle, cancellationToken);

        if (handleTaken)
        {
            return CreateUserResult.Failure([$"Handle '{handle}' is already taken."]);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            Handle = handle,
            NormalizedHandle = normalizedHandle,
            DisplayName = displayName,
        };

        try
        {
            var result = await userManager.CreateAsync(user, password);

            return result.Succeeded
                ? CreateUserResult.Success(user.Id)
                : CreateUserResult.Failure(result.Errors.Select(e => e.Description));
        }
        catch (DbUpdateException ex) when (DbExceptions.IsUniqueViolation(ex))
        {
            // Race backstop: between the AnyAsync pre-check above and this insert, a concurrent register
            // (or a post-trim collision) can slip past and hit the unique Handle index. Convert that to
            // the same friendly failure the pre-check returns, so it surfaces as a clean 400, not a 500.
            return CreateUserResult.Failure([$"Handle '{handle}' is already taken."]);
        }
    }

    public async Task<CredentialValidationResult> ValidateCredentialsAsync(
        string identifier,
        string password,
        CancellationToken cancellationToken = default)
    {
        // Twitter-style: the identifier may be an email or an @handle. Treat it as an email only when it
        // has real email shape (an @ with a dot in the part after it); otherwise resolve it as a handle
        // (case-insensitive, leading @ optional) via the normalized column.
        ApplicationUser? user;
        if (LooksLikeEmail(identifier))
        {
            user = await userManager.FindByEmailAsync(identifier);
        }
        else
        {
            var normalizedHandle = HandleNormalizer.Normalize(identifier);
            user = await userManager.Users
                .FirstOrDefaultAsync(u => u.NormalizedHandle == normalizedHandle, cancellationToken);
        }

        if (user is null)
        {
            // Equalize timing with the found-user path so callers can't enumerate accounts by latency.
            DummyHasher.VerifyHashedPassword(DummyUser, DummyPasswordHash, password);
            return CredentialValidationResult.Invalid;
        }

        // Lockout (brute-force defense), driven through UserManager's lockout primitives — the same
        // machinery SignInManager.CheckPasswordSignInAsync uses, but without dragging the ASP.NET Core
        // web framework into this class library (consistent with the cookieless AddIdentityCore setup).
        if (await userManager.IsLockedOutAsync(user))
        {
            return CredentialValidationResult.LockedOut;
        }

        if (await userManager.CheckPasswordAsync(user, password))
        {
            // Successful login clears any accumulated failed-attempt count.
            await userManager.ResetAccessFailedCountAsync(user);
            return CredentialValidationResult.Success(
                new AuthUser(user.Id, user.Email!, user.Handle, user.DisplayName, user.AvatarUrl));
        }

        // Wrong password: count the failure; if it crosses the threshold the account is now locked.
        await userManager.AccessFailedAsync(user);
        return await userManager.IsLockedOutAsync(user)
            ? CredentialValidationResult.LockedOut
            : CredentialValidationResult.Invalid;
    }

    public async Task<AuthUser?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        return user is null
            ? null
            : new AuthUser(user.Id, user.Email!, user.Handle, user.DisplayName, user.AvatarUrl);
    }

    public async Task<AuthUser?> UpdateProfileAsync(
        Guid userId,
        string displayName,
        string? bio,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        user.DisplayName = displayName;
        user.Bio = bio;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            // The fields are length-validated upstream, so a failure here is unexpected; surface it rather
            // than silently swallowing it.
            throw new InvalidOperationException(
                $"Failed to update profile: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        return new AuthUser(user.Id, user.Email!, user.Handle, user.DisplayName, user.AvatarUrl);
    }

    public async Task<AvatarMutationResult?> UpdateAvatarAsync(
        Guid userId,
        string avatarUrl,
        string avatarPublicId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        // Capture the asset being replaced before overwriting, so the caller can delete it.
        var previousPublicId = user.AvatarPublicId;
        user.AvatarUrl = avatarUrl;
        user.AvatarPublicId = avatarPublicId;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to update avatar: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        return new AvatarMutationResult(
            new AuthUser(user.Id, user.Email!, user.Handle, user.DisplayName, user.AvatarUrl),
            previousPublicId);
    }

    public async Task<AvatarMutationResult?> ClearAvatarAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return null;
        }

        var previousPublicId = user.AvatarPublicId;
        user.AvatarUrl = null;
        user.AvatarPublicId = null;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to clear avatar: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        return new AvatarMutationResult(
            new AuthUser(user.Id, user.Email!, user.Handle, user.DisplayName, user.AvatarUrl),
            previousPublicId);
    }
}
