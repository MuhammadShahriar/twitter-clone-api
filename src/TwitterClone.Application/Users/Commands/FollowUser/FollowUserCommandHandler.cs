using FluentValidation;
using FluentValidation.Results;
using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Users.Commands.FollowUser;

public class FollowUserCommandHandler(
    IUserRepository userRepository,
    IFollowRepository followRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<FollowUserCommand, UserDto>
{
    public async Task<UserDto> Handle(FollowUserCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var followerId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot follow a user without an authenticated user.");

        var followeeId = await userRepository.GetIdByHandleAsync(request.Handle, cancellationToken)
            ?? throw new NotFoundException("User", request.Handle);

        // You cannot follow yourself — surfaces as a 400 via the validation pipeline. Compared by id (not
        // handle string) so it holds regardless of handle casing/formatting.
        if (followeeId == followerId)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(FollowUserCommand.Handle), "You cannot follow yourself.")]);
        }

        // Idempotent: only stage a new edge if one doesn't already exist. The unique (FollowerId,
        // FolloweeId) index is the DB-level backstop — if a concurrent follow wins the race and inserts
        // first, swallow the unique violation and treat this call as the success it effectively is.
        var existing = await followRepository.FindAsync(followerId, followeeId, cancellationToken);
        if (existing is null)
        {
            try
            {
                await followRepository.AddAsync(new Follow(followerId, followeeId), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UniqueConstraintViolationException)
            {
                // A concurrent follow already inserted the edge — the follow exists either way.
            }
        }

        // Re-read so the response carries the updated followerCount and isFollowedByCurrentUser = true.
        var dto = await userRepository.GetByHandleAsync(request.Handle, followerId, cancellationToken);
        return dto!;
    }
}
