using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Users.Commands.UpdateAvatar;

public class UpdateAvatarCommandHandler(
    IImageStorageService imageStorage,
    IIdentityService identityService,
    IUserRepository userRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateAvatarCommand, UserDto>
{
    public async Task<UserDto> Handle(UpdateAvatarCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a caller; this guard is defensive (the avatar is the caller's).
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot update an avatar without an authenticated user.");

        // Backend-proxied upload: the bytes go client → API → image host, so the storage secret never reaches
        // the browser. Validation has already capped the size/type, so nothing invalid reaches the host.
        var uploaded = await imageStorage.UploadAsync(request.Image, cancellationToken);

        var updated = await identityService.UpdateAvatarAsync(userId, uploaded.Url, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        // Re-read the full lite profile so the response matches GET /api/users/{handle} (now with the avatar).
        var dto = await userRepository.GetByHandleAsync(updated.Handle, userId, cancellationToken);
        return dto!;
    }
}
