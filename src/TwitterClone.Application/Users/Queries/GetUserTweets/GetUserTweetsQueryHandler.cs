using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Users.Queries.GetUserTweets;

public class GetUserTweetsQueryHandler(
    IUserRepository userRepository,
    ITweetRepository tweetRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetUserTweetsQuery, CursorPage<TweetDto>>
{
    public async Task<CursorPage<TweetDto>> Handle(GetUserTweetsQuery request, CancellationToken cancellationToken)
    {
        // Resolve the handle to a user id; an unknown handle is a 404 (not an empty page).
        var authorId = await userRepository.GetIdByHandleAsync(request.Handle, cancellationToken)
            ?? throw new NotFoundException("User", request.Handle);

        // Public read; currentUser.UserId is null for an anonymous reader (then the by-me flags are false).
        return await tweetRepository.GetUserTweetsAsync(
            authorId,
            currentUser.UserId,
            request.Cursor,
            PaginationDefaults.Clamp(request.Limit),
            cancellationToken);
    }
}
