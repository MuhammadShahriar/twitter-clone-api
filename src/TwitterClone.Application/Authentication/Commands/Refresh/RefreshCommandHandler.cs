using MediatR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Authentication.Commands.Refresh;

public class RefreshCommandHandler(
    IRefreshTokenService refreshTokenService,
    IRefreshTokenRepository refreshTokens,
    IIdentityService identityService,
    IJwtTokenGenerator tokenGenerator,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RefreshCommand, AuthTokens?>
{
    public async Task<AuthTokens?> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var presentedHash = refreshTokenService.Hash(request.RefreshToken);
        var token = await refreshTokens.GetByHashAsync(presentedHash, cancellationToken);

        if (token is null)
        {
            return null; // unknown token
        }

        // Reuse detection: a token that has already been revoked (rotated away or logged out) being
        // presented again means it was likely stolen → revoke the entire family and reject.
        if (token.IsRevoked)
        {
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (token.IsExpired(now))
        {
            token.Revoke(now);
            refreshTokens.Update(token);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }

        var user = await identityService.GetByIdAsync(token.UserId, cancellationToken);
        if (user is null)
        {
            // Owner vanished — burn the family rather than mint a token for a ghost.
            await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }

        // Rotate: revoke the presented token and issue a successor in the same family.
        var replacement = refreshTokenService.Generate();
        token.Revoke(now, replacement.TokenHash);
        refreshTokens.Update(token);

        var newToken = new RefreshToken
        {
            UserId = token.UserId,
            FamilyId = token.FamilyId,
            TokenHash = replacement.TokenHash,
            ExpiresAtUtc = replacement.ExpiresAtUtc,
        };
        await refreshTokens.AddAsync(newToken, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = tokenGenerator.Generate(user);
        var result = new AuthenticationResult(
            accessToken.Value, accessToken.ExpiresAtUtc, user.Id, user.Handle, user.DisplayName);

        return new AuthTokens(result, replacement.RawToken, replacement.ExpiresAtUtc);
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken)
    {
        var family = await refreshTokens.GetByFamilyAsync(familyId, cancellationToken);
        foreach (var member in family.Where(t => !t.IsRevoked))
        {
            member.Revoke(now);
            refreshTokens.Update(member);
        }
    }
}
