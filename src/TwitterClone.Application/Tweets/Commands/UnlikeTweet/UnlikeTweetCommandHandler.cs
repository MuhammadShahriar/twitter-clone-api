using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Tweets.Commands.UnlikeTweet;

public class UnlikeTweetCommandHandler(
    ITweetRepository tweetRepository,
    ILikeRepository likeRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<UnlikeTweetCommand, TweetDto>
{
    public async Task<TweetDto> Handle(UnlikeTweetCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot unlike a tweet without an authenticated user.");

        if (!await tweetRepository.ExistsAsync(request.TweetId, cancellationToken))
        {
            throw new NotFoundException(nameof(Tweet), request.TweetId);
        }

        // Idempotent: only stage a removal if a like actually exists. If a concurrent unlike removed the row
        // first, swallow the resulting concurrency conflict — the tweet is unliked either way.
        var existing = await likeRepository.FindAsync(userId, request.TweetId, cancellationToken);
        if (existing is not null)
        {
            try
            {
                likeRepository.Remove(existing);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                // A concurrent unlike already removed the row — nothing left to do.
            }
        }

        // Re-read so the response carries the updated likeCount and likedByCurrentUser = false.
        var dto = await tweetRepository.GetByIdWithAuthorAsync(request.TweetId, userId, cancellationToken);
        return dto!;
    }
}
