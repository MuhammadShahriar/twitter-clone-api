using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Users.Commands.UnfollowUser;

public class UnfollowUserCommandHandler(
    IUserRepository userRepository,
    IFollowRepository followRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<UnfollowUserCommand, UserDto>
{
    public async Task<UserDto> Handle(UnfollowUserCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var followerId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot unfollow a user without an authenticated user.");

        var followeeId = await userRepository.GetIdByHandleAsync(request.Handle, cancellationToken)
            ?? throw new NotFoundException("User", request.Handle);

        // Idempotent: only stage a removal if the edge actually exists. If a concurrent unfollow removed the
        // edge first, swallow the resulting concurrency conflict — the follow is gone either way.
        var existing = await followRepository.FindAsync(followerId, followeeId, cancellationToken);
        if (existing is not null)
        {
            try
            {
                followRepository.Remove(existing);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                // A concurrent unfollow already removed the edge — nothing left to do.
            }
        }

        // Re-read so the response carries the updated followerCount and isFollowedByCurrentUser = false.
        var dto = await userRepository.GetByHandleAsync(request.Handle, followerId, cancellationToken);
        return dto!;
    }
}
