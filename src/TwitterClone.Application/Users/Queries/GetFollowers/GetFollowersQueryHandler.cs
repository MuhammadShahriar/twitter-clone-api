using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;

namespace TwitterClone.Application.Users.Queries.GetFollowers;

public class GetFollowersQueryHandler(
    IUserRepository userRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetFollowersQuery, CursorPage<UserDto>>
{
    public async Task<CursorPage<UserDto>> Handle(GetFollowersQuery request, CancellationToken cancellationToken)
    {
        // Resolve the handle to a user id; an unknown handle is a 404 (not an empty page).
        var userId = await userRepository.GetIdByHandleAsync(request.Handle, cancellationToken)
            ?? throw new NotFoundException("User", request.Handle);

        // Public read; currentUser.UserId is null for an anonymous reader (then the by-me flags are false).
        return await userRepository.GetFollowersAsync(
            userId,
            currentUser.UserId,
            request.Cursor,
            PaginationDefaults.Clamp(request.Limit),
            cancellationToken);
    }
}
