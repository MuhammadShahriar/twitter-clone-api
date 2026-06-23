using MediatR;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Users.Queries.GetUserByHandle;

public class GetUserByHandleQueryHandler(IUserRepository userRepository, ICurrentUserService currentUser)
    : IRequestHandler<GetUserByHandleQuery, UserDto?>
{
    public async Task<UserDto?> Handle(GetUserByHandleQuery request, CancellationToken cancellationToken) =>
        // Public read; currentUser.UserId is null for an anonymous reader (isFollowedByCurrentUser → false).
        await userRepository.GetByHandleAsync(request.Handle, currentUser.UserId, cancellationToken);
}
