using MediatR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Tweets.Queries.GetTweetById;

public class GetTweetByIdQueryHandler(ITweetRepository tweetRepository, ICurrentUserService currentUser)
    : IRequestHandler<GetTweetByIdQuery, TweetDto?>
{
    public async Task<TweetDto?> Handle(GetTweetByIdQuery request, CancellationToken cancellationToken) =>
        // Public read; currentUser.UserId is null for an anonymous reader (flags come back false).
        await tweetRepository.GetByIdWithAuthorAsync(request.Id, currentUser.UserId, cancellationToken);
}
