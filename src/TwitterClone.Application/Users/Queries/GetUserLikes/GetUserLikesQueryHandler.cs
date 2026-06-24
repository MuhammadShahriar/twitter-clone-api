using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Users.Queries.GetUserLikes;

public class GetUserLikesQueryHandler(
    IUserRepository userRepository,
    ITweetRepository tweetRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetUserLikesQuery, CursorPage<TweetDto>>
{
    public async Task<CursorPage<TweetDto>> Handle(GetUserLikesQuery request, CancellationToken cancellationToken)
    {
        // Resolve the handle to a user id; an unknown handle is a 404 (not an empty page).
        var likerId = await userRepository.GetIdByHandleAsync(request.Handle, cancellationToken)
            ?? throw new NotFoundException("User", request.Handle);

        // Public read; currentUser.UserId is null for an anonymous reader (then the by-me flags are false).
        return await tweetRepository.GetUserLikedTweetsAsync(
            likerId,
            currentUser.UserId,
            request.Cursor,
            PaginationDefaults.Clamp(request.Limit),
            cancellationToken);
    }
}
