using MediatR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Tweets.Queries.GetTweetById;

public class GetTweetByIdQueryHandler(ITweetRepository tweetRepository)
    : IRequestHandler<GetTweetByIdQuery, TweetDto?>
{
    public async Task<TweetDto?> Handle(GetTweetByIdQuery request, CancellationToken cancellationToken)
    {
        var tweet = await tweetRepository.GetByIdAsync(request.Id, cancellationToken);
        return tweet is null ? null : TweetDto.FromEntity(tweet);
    }
}
