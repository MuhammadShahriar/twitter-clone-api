using MediatR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Feed.Queries.GetFollowingFeed;

public class GetFollowingFeedQueryHandler(ITweetRepository tweetRepository, ICurrentUserService currentUser)
    : IRequestHandler<GetFollowingFeedQuery, CursorPage<TweetDto>>
{
    public async Task<CursorPage<TweetDto>> Handle(GetFollowingFeedQuery request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive (the feed is personal).
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("The Following feed requires an authenticated user.");

        return await tweetRepository.GetFollowingFeedAsync(
            userId,
            request.Cursor,
            PaginationDefaults.Clamp(request.Limit),
            cancellationToken);
    }
}
