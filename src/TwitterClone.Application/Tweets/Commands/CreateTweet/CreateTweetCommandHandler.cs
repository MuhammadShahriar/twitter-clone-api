using MediatR;
using TwitterClone.Application.Common;
using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Domain.Entities;
using TwitterClone.Domain.Enums;

namespace TwitterClone.Application.Tweets.Commands.CreateTweet;

public class CreateTweetCommandHandler(
    ITweetRepository tweetRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    IImageStorageService imageStorage,
    INotificationService notifications)
    : IRequestHandler<CreateTweetCommand, TweetDto>
{
    public async Task<TweetDto> Handle(CreateTweetCommand request, CancellationToken cancellationToken)
    {
        // The author is the authenticated caller. The controller's [Authorize] guarantees a user is
        // present; this guard is defensive in case the handler is ever invoked outside that pipeline.
        var authorId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot create a tweet without an authenticated user.");

        // A quote must reference a tweet that exists. Resolve the quoted author up front (one query that also
        // serves the Quote notification below): a missing quoted tweet is a 404 — surfaced BEFORE any image
        // upload so a bad id fails fast without orphaning uploads. (Content-required is enforced by the
        // validator → 400.) The validator already guarantees the quoted tweet isn't FK-checked as a reply.
        Guid? quotedAuthorId = null;
        if (request.QuotedTweetId is Guid quotedId)
        {
            quotedAuthorId = await tweetRepository.GetAuthorIdAsync(quotedId, cancellationToken)
                ?? throw new NotFoundException(nameof(Tweet), quotedId);
        }

        var tweet = new Tweet(request.Content.Trim(), authorId, request.ParentId, request.QuotedTweetId);

        // Backend-proxied upload: push each attached image to the image host (the secret stays server-side)
        // and record the returned URL/publicId on the tweet, preserving attachment order. Validation has
        // already capped the count/size/type, so nothing invalid reaches the host.
        if (request.Images is { Count: > 0 })
        {
            foreach (var image in request.Images)
            {
                var uploaded = await imageStorage.UploadAsync(image, cancellationToken);
                tweet.AddMedia(uploaded.Url, uploaded.PublicId);
            }
        }

        // The repository only stages the insert (tweet + its media); the unit of work commits it.
        await tweetRepository.AddAsync(tweet, cancellationToken);

        // If this tweet is a reply, notify the parent tweet's author that they were replied to (self-replies
        // are skipped inside the service). The parent was already validated to exist; staged here so the
        // notification commits in the same SaveChanges as the reply. TweetId points at the reply itself.
        Guid? repliedToAuthorId = null;
        if (tweet.ParentId is Guid parentId)
        {
            var parentAuthorId = await tweetRepository.GetAuthorIdAsync(parentId, cancellationToken);
            if (parentAuthorId is Guid recipient)
            {
                await notifications.CreateAsync(
                    recipient, authorId, NotificationType.Reply, tweet.Id, cancellationToken);
                repliedToAuthorId = recipient;
            }
        }

        // If this is a quote tweet, notify the quoted tweet's author. Self-quote is skipped inside the service
        // (recipient == actor), and the unread-dedup applies as usual. TweetId points at the quote tweet, so
        // the click-through target is the quote (not the quoted original). Resolved once, above.
        if (quotedAuthorId is Guid quotedRecipient)
        {
            await notifications.CreateAsync(
                quotedRecipient, authorId, NotificationType.Quote, tweet.Id, cancellationToken);
        }

        // Notify each @handle mentioned in the text. The service already skips self-mentions (recipient ==
        // actor) and de-dups equivalent unread, and unknown handles never resolve to an id — so "@author",
        // "@nobody" and a repeated "@alice @alice" all behave. We additionally skip the reply's parent author
        // AND the quoted tweet's author: a reply/quote that @-mentions the very person it replies to/quotes
        // should yield only the Reply/Quote, not also a Mention (different notification types, so the service
        // wouldn't collapse them). Third-party mentions still notify. TweetId points at this tweet so it previews.
        var mentionedHandles = MentionParser.ExtractHandles(request.Content);
        if (mentionedHandles.Count > 0)
        {
            var mentionedIds = await userRepository.GetIdsByHandlesAsync(mentionedHandles, cancellationToken);
            foreach (var mentionedId in mentionedIds)
            {
                if (mentionedId == repliedToAuthorId || mentionedId == quotedAuthorId)
                {
                    continue;
                }

                await notifications.CreateAsync(
                    mentionedId, authorId, NotificationType.Mention, tweet.Id, cancellationToken);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read through the author join so the response carries the handle/display name (and the
        // zeroed engagement counts/flags). The author is the caller, so pass their id for the flags.
        var dto = await tweetRepository.GetByIdWithAuthorAsync(tweet.Id, authorId, cancellationToken);
        return dto!;
    }
}
