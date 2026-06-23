using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Tweets.Commands.LikeTweet;

public class LikeTweetCommandHandler(
    ITweetRepository tweetRepository,
    ILikeRepository likeRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<LikeTweetCommand, TweetDto>
{
    public async Task<TweetDto> Handle(LikeTweetCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot like a tweet without an authenticated user.");

        if (!await tweetRepository.ExistsAsync(request.TweetId, cancellationToken))
        {
            throw new NotFoundException(nameof(Tweet), request.TweetId);
        }

        // Idempotent: only stage a new like if the user hasn't already liked this tweet. The unique
        // (UserId, TweetId) index is the DB-level backstop — if a concurrent like wins the race and inserts
        // first, swallow the unique violation and treat this call as the success it effectively is.
        var existing = await likeRepository.FindAsync(userId, request.TweetId, cancellationToken);
        if (existing is null)
        {
            try
            {
                await likeRepository.AddAsync(new Like(userId, request.TweetId), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UniqueConstraintViolationException)
            {
                // A concurrent like already inserted the row — the tweet is liked either way.
            }
        }

        // Re-read so the response carries the updated likeCount and likedByCurrentUser = true.
        var dto = await tweetRepository.GetByIdWithAuthorAsync(request.TweetId, userId, cancellationToken);
        return dto!;
    }
}
