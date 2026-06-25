using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Tweets.Commands.UnbookmarkTweet;

public class UnbookmarkTweetCommandHandler(
    ITweetRepository tweetRepository,
    IBookmarkRepository bookmarkRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<UnbookmarkTweetCommand, TweetDto>
{
    public async Task<TweetDto> Handle(UnbookmarkTweetCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot un-bookmark a tweet without an authenticated user.");

        if (!await tweetRepository.ExistsAsync(request.TweetId, cancellationToken))
        {
            throw new NotFoundException(nameof(Tweet), request.TweetId);
        }

        // Idempotent: only stage a removal if a bookmark actually exists. If a concurrent un-bookmark removed
        // the row first, swallow the resulting concurrency conflict — the tweet is un-bookmarked either way.
        var existing = await bookmarkRepository.FindAsync(userId, request.TweetId, cancellationToken);
        if (existing is not null)
        {
            try
            {
                bookmarkRepository.Remove(existing);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                // A concurrent un-bookmark already removed the row — nothing left to do.
            }
        }

        // Re-read so the response carries bookmarkedByCurrentUser = false.
        var dto = await tweetRepository.GetByIdWithAuthorAsync(request.TweetId, userId, cancellationToken);
        return dto!;
    }
}
