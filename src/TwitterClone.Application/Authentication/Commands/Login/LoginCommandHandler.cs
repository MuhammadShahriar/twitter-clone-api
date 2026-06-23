using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Authentication.Commands.Login;

public class LoginCommandHandler(
    IIdentityService identityService,
    IJwtTokenGenerator tokenGenerator,
    IRefreshTokenService refreshTokenService,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LoginCommand, AuthTokens?>
{
    public async Task<AuthTokens?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validation = await identityService.ValidateCredentialsAsync(
            request.Email.Trim(), request.Password, cancellationToken);

        if (validation.Status == CredentialValidationStatus.LockedOut)
        {
            // Too many failed attempts → 423 Locked (via AccountLockedExceptionHandler).
            throw new AccountLockedException();
        }

        if (validation.Status != CredentialValidationStatus.Success || validation.User is null)
        {
            return null; // invalid credentials → controller returns 401
        }

        var user = validation.User;
        var accessToken = tokenGenerator.Generate(user);

        // A successful login starts a brand-new refresh-token family.
        var refresh = refreshTokenService.Generate();
        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            FamilyId = Guid.NewGuid(),
            TokenHash = refresh.TokenHash,
            ExpiresAtUtc = refresh.ExpiresAtUtc,
        };

        await refreshTokens.AddAsync(refreshToken, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var result = new AuthenticationResult(
            accessToken.Value, accessToken.ExpiresAtUtc, user.Id, user.Handle, user.DisplayName);

        return new AuthTokens(result, refresh.RawToken, refresh.ExpiresAtUtc);
    }
}
