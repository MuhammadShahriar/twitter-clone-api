using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Tweets.Commands.DeleteTweet;

public class DeleteTweetCommandHandler(
    ITweetRepository tweetRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteTweetCommand>
{
    public async Task Handle(DeleteTweetCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot delete a tweet without an authenticated user.");

        var tweet = await tweetRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Tweet), request.Id);

        // Author-only: surfaces as 403 (the tweet exists, it's just not the caller's to delete) — not 404.
        if (tweet.AuthorId != userId)
        {
            throw new ForbiddenAccessException();
        }

        // Cascade to direct replies explicitly so the behaviour holds on the in-memory test provider too
        // (which does not enforce the DB-level cascade). The DB cascade still cleans up any deeper chain.
        var replies = await tweetRepository.GetDirectRepliesAsync(request.Id, cancellationToken);
        foreach (var reply in replies)
        {
            tweetRepository.Remove(reply);
        }

        tweetRepository.Remove(tweet);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
