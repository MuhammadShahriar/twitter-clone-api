using MediatR;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;

namespace TwitterClone.Application.Tweets.Commands.EditTweet;

public class EditTweetCommandHandler(
    ITweetRepository tweetRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    TweetEditSettings editSettings)
    : IRequestHandler<EditTweetCommand, TweetDto>
{
    public async Task<TweetDto> Handle(EditTweetCommand request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive.
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot edit a tweet without an authenticated user.");

        var tweet = await tweetRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Tweet), request.Id);

        // Author-only — surfaces as 403 (the tweet exists, it's just not the caller's to edit), like delete.
        if (tweet.AuthorId != userId)
        {
            throw new ForbiddenAccessException();
        }

        // Time window — only editable within EditWindow of posting. Past that it's a 409 Conflict (the caller
        // is the rightful author, so it isn't a 403; the tweet's age conflicts with the edit). CreatedAtUtc is
        // never altered by an edit, so the window is measured from the original post time.
        var now = DateTime.UtcNow;
        if (now - tweet.CreatedAtUtc > editSettings.EditWindow)
        {
            throw new EditWindowExpiredException(editSettings.EditWindow);
        }

        // Apply the text change and stamp the edit time. Mentions are deliberately NOT re-parsed/re-notified
        // (v1): the original mentions were notified at create; new mentions added by an edit don't notify.
        tweet.Edit(request.Content.Trim(), now);
        tweetRepository.Update(tweet);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read through the author join so the response carries the handle/counts/flags (and editedAtUtc).
        var dto = await tweetRepository.GetByIdWithAuthorAsync(tweet.Id, userId, cancellationToken);
        return dto!;
    }
}
