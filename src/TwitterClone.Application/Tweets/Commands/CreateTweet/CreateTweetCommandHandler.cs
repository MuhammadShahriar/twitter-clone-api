using MediatR;
using TwitterClone.Application.Common;
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

        var tweet = new Tweet(request.Content.Trim(), authorId, request.ParentId);

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

        // Notify each @handle mentioned in the text. The service already skips self-mentions (recipient ==
        // actor) and de-dups equivalent unread, and unknown handles never resolve to an id — so "@author",
        // "@nobody" and a repeated "@alice @alice" all behave. We additionally skip the reply's parent author:
        // a reply that @-mentions the very person it replies to should yield only the Reply, not also a Mention
        // (the two are different notification types, so the service wouldn't collapse them). TweetId points at
        // this tweet/reply so the notification can preview it.
        var mentionedHandles = MentionParser.ExtractHandles(request.Content);
        if (mentionedHandles.Count > 0)
        {
            var mentionedIds = await userRepository.GetIdsByHandlesAsync(mentionedHandles, cancellationToken);
            foreach (var mentionedId in mentionedIds)
            {
                if (mentionedId == repliedToAuthorId)
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
