using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Tweets.Commands.BookmarkTweet;

public class BookmarkTweetCommandHandler(
    ITweetRepository tweetRepository,
    IBookmarkRepository bookmarkRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<BookmarkTweetCommand, TweetDto>
{
    public async Task<TweetDto> Handle(BookmarkTweetCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot bookmark a tweet without an authenticated user.");

        if (!await tweetRepository.ExistsAsync(request.TweetId, cancellationToken))
        {
            throw new NotFoundException(nameof(Tweet), request.TweetId);
        }

        // Idempotent: only stage a new bookmark if the user hasn't already bookmarked this tweet. The unique
        // (UserId, TweetId) index is the DB-level backstop — if a concurrent bookmark wins the race and inserts
        // first, swallow the unique violation and treat this call as the success it effectively is. Unlike a
        // like, no notification is raised (bookmarks are private).
        var existing = await bookmarkRepository.FindAsync(userId, request.TweetId, cancellationToken);
        if (existing is null)
        {
            try
            {
                await bookmarkRepository.AddAsync(new Bookmark(userId, request.TweetId), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (UniqueConstraintViolationException)
            {
                // A concurrent bookmark already inserted the row — the tweet is bookmarked either way.
            }
        }

        // Re-read so the response carries bookmarkedByCurrentUser = true.
        var dto = await tweetRepository.GetByIdWithAuthorAsync(request.TweetId, userId, cancellationToken);
        return dto!;
    }
}
