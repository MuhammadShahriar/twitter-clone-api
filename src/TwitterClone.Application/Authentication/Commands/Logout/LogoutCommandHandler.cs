using MediatR;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Authentication.Commands.Logout;

public class LogoutCommandHandler(
    IRefreshTokenService refreshTokenService,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return; // nothing to revoke
        }

        var hash = refreshTokenService.Hash(request.RefreshToken);
        var token = await refreshTokens.GetByHashAsync(hash, cancellationToken);

        if (token is null)
        {
            return; // unknown token — treat as already logged out
        }

        // Revoke the whole family so no token in this lineage can be refreshed afterwards.
        var now = DateTime.UtcNow;
        var family = await refreshTokens.GetByFamilyAsync(token.FamilyId, cancellationToken);
        foreach (var member in family.Where(t => !t.IsRevoked))
        {
            member.Revoke(now);
            refreshTokens.Update(member);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
