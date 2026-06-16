using MediatR;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Tweets.Queries.GetTweets;

public class GetTweetsQueryHandler(ITweetRepository tweetRepository)
    : IRequestHandler<GetTweetsQuery, IReadOnlyList<TweetDto>>
{
    public async Task<IReadOnlyList<TweetDto>> Handle(GetTweetsQuery request, CancellationToken cancellationToken)
    {
        var tweets = await tweetRepository.GetAllNewestFirstAsync(cancellationToken);
        return tweets.Select(TweetDto.FromEntity).ToList();
    }
}
