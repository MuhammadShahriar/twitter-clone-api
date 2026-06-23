using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Tweets.Commands.UnretweetTweet;

public class UnretweetTweetCommandHandler(
    ITweetRepository tweetRepository,
    IRetweetRepository retweetRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<UnretweetTweetCommand, TweetDto>
{
    public async Task<TweetDto> Handle(UnretweetTweetCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot unretweet a tweet without an authenticated user.");

        if (!await tweetRepository.ExistsAsync(request.TweetId, cancellationToken))
        {
            throw new NotFoundException(nameof(Tweet), request.TweetId);
        }

        // Idempotent: only stage a removal if a retweet actually exists. If a concurrent unretweet removed
        // the row first, swallow the resulting concurrency conflict — the tweet is unretweeted either way.
        var existing = await retweetRepository.FindAsync(userId, request.TweetId, cancellationToken);
        if (existing is not null)
        {
            try
            {
                retweetRepository.Remove(existing);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                // A concurrent unretweet already removed the row — nothing left to do.
            }
        }

        // Re-read so the response carries the updated retweetCount and retweetedByCurrentUser = false.
        var dto = await tweetRepository.GetByIdWithAuthorAsync(request.TweetId, userId, cancellationToken);
        return dto!;
    }
}
