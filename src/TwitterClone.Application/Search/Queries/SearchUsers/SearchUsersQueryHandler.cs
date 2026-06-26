using MediatR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Users;

namespace TwitterClone.Application.Search.Queries.SearchUsers;

public class SearchUsersQueryHandler(
    IUserRepository userRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<SearchUsersQuery, CursorPage<UserDto>>
{
    public async Task<CursorPage<UserDto>> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
    {
        // A blank query matches nothing — return an empty page rather than every user.
        var term = request.Q?.Trim();
        if (string.IsNullOrEmpty(term))
        {
            return new CursorPage<UserDto>([], null);
        }

        // Public read; currentUser.UserId is null for an anonymous reader (then the by-me flag is false).
        return await userRepository.SearchAsync(
            term,
            currentUser.UserId,
            request.Cursor,
            PaginationDefaults.Clamp(request.Limit),
            cancellationToken);
    }
}
