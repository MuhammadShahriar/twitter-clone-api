using MediatR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Tweets.Queries.GetTweets;

public class GetTweetsQueryHandler(ITweetRepository tweetRepository, ICurrentUserService currentUser)
    : IRequestHandler<GetTweetsQuery, CursorPage<TweetDto>>
{
    public async Task<CursorPage<TweetDto>> Handle(GetTweetsQuery request, CancellationToken cancellationToken) =>
        // The feed is public; currentUser.UserId is null for an anonymous reader (then the like/retweet
        // flags come back false) and the caller's id otherwise.
        await tweetRepository.GetFeedAsync(
            currentUser.UserId,
            request.Cursor,
            PaginationDefaults.Clamp(request.Limit),
            cancellationToken);
}
