using MediatR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Tweets.Queries.GetReplies;

public class GetRepliesQueryHandler(ITweetRepository tweetRepository, ICurrentUserService currentUser)
    : IRequestHandler<GetRepliesQuery, CursorPage<TweetDto>>
{
    public async Task<CursorPage<TweetDto>> Handle(GetRepliesQuery request, CancellationToken cancellationToken) =>
        // Replies are public; currentUser.UserId is null for an anonymous reader (flags come back false).
        await tweetRepository.GetRepliesAsync(
            request.ParentId,
            currentUser.UserId,
            request.Cursor,
            PaginationDefaults.Clamp(request.Limit),
            cancellationToken);
}
