using MediatR;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Authentication.Queries.GetCurrentUser;

public class GetCurrentUserQueryHandler(ICurrentUserService currentUser, IIdentityService identityService)
    : IRequestHandler<GetCurrentUserQuery, CurrentUserDto?>
{
    public async Task<CurrentUserDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null)
        {
            return null; // anonymous — controller's [Authorize] normally prevents reaching here
        }

        // Re-read the canonical profile rather than trusting the token's snapshot.
        var user = await identityService.GetByIdAsync(userId.Value, cancellationToken);

        return user is null
            ? null
            : new CurrentUserDto(user.Id, user.Email, user.Handle, user.DisplayName, user.AvatarUrl);
    }
}
