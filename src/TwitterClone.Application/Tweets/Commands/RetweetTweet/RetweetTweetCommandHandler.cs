using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Tweets.Commands.RetweetTweet;

public class RetweetTweetCommandHandler(
    ITweetRepository tweetRepository,
    IRetweetRepository retweetRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<RetweetTweetCommand, TweetDto>
{
    public async Task<TweetDto> Handle(RetweetTweetCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot retweet a tweet without an authenticated user.");

        if (!await tweetRepository.ExistsAsync(request.TweetId, cancellationToken))
        {
            throw new NotFoundException(nameof(Tweet), request.TweetId);
        }

        // Idempotent: only stage a new retweet if the user hasn't already retweeted this tweet. The unique
        // (UserId, TweetId) index is the DB-level backstop — if a concurrent retweet wins the race and
        // inserts first, swallow the unique violation and treat this call as the success it effectively is.
        var existing = await retweetRepository.FindAsync(userId, request.TweetId, cancellationToken);
        if (existing is null)
        {
            try
            {
                await retweetRepository.AddAsync(new Retweet(userId, request.TweetId), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UniqueConstraintViolationException)
            {
                // A concurrent retweet already inserted the row — the tweet is retweeted either way.
            }
        }

        // Re-read so the response carries the updated retweetCount and retweetedByCurrentUser = true.
        var dto = await tweetRepository.GetByIdWithAuthorAsync(request.TweetId, userId, cancellationToken);
        return dto!;
    }
}
