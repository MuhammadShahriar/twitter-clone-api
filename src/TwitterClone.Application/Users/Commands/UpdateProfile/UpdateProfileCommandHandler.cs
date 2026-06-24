using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Users.Commands.UpdateProfile;

public class UpdateProfileCommandHandler(
    IIdentityService identityService,
    IUserRepository userRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateProfileCommand, UserDto>
{
    public async Task<UserDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a caller; this guard is defensive (a profile edit is personal).
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot update a profile without an authenticated user.");

        var updated = await identityService.UpdateProfileAsync(
            userId, request.DisplayName.Trim(), request.Bio?.Trim(), cancellationToken)
            ?? throw new NotFoundException("User", userId);

        // Re-read the full lite profile (with counts + the caller's own follow flag) so the response matches
        // GET /api/users/{handle}. The caller views their own profile, so pass their id for the flag.
        var dto = await userRepository.GetByHandleAsync(updated.Handle, userId, cancellationToken);
        return dto!;
    }
}
