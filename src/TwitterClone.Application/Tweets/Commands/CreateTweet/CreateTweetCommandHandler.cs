using MediatR;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Tweets.Commands.CreateTweet;

public class CreateTweetCommandHandler(ITweetRepository tweetRepository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateTweetCommand, TweetDto>
{
    public async Task<TweetDto> Handle(CreateTweetCommand request, CancellationToken cancellationToken)
    {
        var tweet = new Tweet
        {
            Content = request.Content.Trim(),
            AuthorHandle = request.AuthorHandle.Trim(),
        };

        // The repository only stages the insert; the unit of work commits it.
        await tweetRepository.AddAsync(tweet, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return TweetDto.FromEntity(tweet);
    }
}
