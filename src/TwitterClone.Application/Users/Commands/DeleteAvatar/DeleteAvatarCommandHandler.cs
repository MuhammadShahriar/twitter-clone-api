using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Users.Commands.DeleteAvatar;

public class DeleteAvatarCommandHandler(
    IImageStorageService imageStorage,
    IIdentityService identityService,
    IUserRepository userRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteAvatarCommand, UserDto>
{
    public async Task<UserDto> Handle(DeleteAvatarCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a caller; this guard is defensive (the avatar is the caller's).
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot remove an avatar without an authenticated user.");

        var mutation = await identityService.ClearAvatarAsync(userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        // Best-effort: delete the now-orphaned asset from the host. Skipped (no-op) when there was no avatar,
        // which is what makes a repeat delete idempotent.
        if (!string.IsNullOrEmpty(mutation.PreviousPublicId))
        {
            await AvatarCleanup.TryDeleteAsync(imageStorage, mutation.PreviousPublicId, cancellationToken);
        }

        // Re-read the full lite profile so the response matches GET /api/users/{handle} (now avatar-less).
        var dto = await userRepository.GetByHandleAsync(mutation.User.Handle, userId, cancellationToken);
        return dto!;
    }
}
