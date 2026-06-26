using MediatR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Search.Queries.SearchTweets;

public class SearchTweetsQueryHandler(
    ITweetRepository tweetRepository,
    ICurrentUserService currentUser)
    : IRequestHandler<SearchTweetsQuery, CursorPage<TweetDto>>
{
    public async Task<CursorPage<TweetDto>> Handle(SearchTweetsQuery request, CancellationToken cancellationToken)
    {
        // A blank query matches nothing — return an empty page rather than the whole timeline.
        var term = request.Q?.Trim();
        if (string.IsNullOrEmpty(term))
        {
            return new CursorPage<TweetDto>([], null);
        }

        // Public read; currentUser.UserId is null for an anonymous reader (then the by-me flags are false).
        return await tweetRepository.SearchAsync(
            term,
            currentUser.UserId,
            request.Cursor,
            PaginationDefaults.Clamp(request.Limit),
            cancellationToken);
    }
}
