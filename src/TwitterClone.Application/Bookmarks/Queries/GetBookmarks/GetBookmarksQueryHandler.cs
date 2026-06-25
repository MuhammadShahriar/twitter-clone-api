using MediatR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Bookmarks.Queries.GetBookmarks;

public class GetBookmarksQueryHandler(
    ITweetRepository tweetRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<GetBookmarksQuery, CursorPage<TweetDto>>
{
    public async Task<CursorPage<TweetDto>> Handle(GetBookmarksQuery request, CancellationToken cancellationToken)
    {
        // Bookmarks are private: the owner is always the authenticated caller (the controller's [Authorize]
        // guarantees one; this guard is defensive). There is no way to read another user's bookmarks.
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot read bookmarks without an authenticated user.");

        return await tweetRepository.GetBookmarkedTweetsAsync(
            userId,
            request.Cursor,
            PaginationDefaults.Clamp(request.Limit),
            cancellationToken);
    }
}
