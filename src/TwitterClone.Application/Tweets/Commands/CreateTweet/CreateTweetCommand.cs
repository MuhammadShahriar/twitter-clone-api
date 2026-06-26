using MediatR;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Tweets.Commands.CreateTweet;

/// <summary>
/// Creates a new tweet and returns the persisted read model. The author is taken from the
/// authenticated caller (see the handler), never from the request body — so there is no handle here.
/// <para>
/// Pass <see cref="ParentId"/> to post a reply to an existing tweet; leave it null for a top-level tweet.
/// The validator checks the parent exists.
/// </para>
/// <para>
/// <see cref="Images"/> are images to attach (backend-proxied upload to the image host); the validator caps
/// the count/size/type. The API layer reads the multipart files into these provider-free models.
/// </para>
/// </summary>
public record CreateTweetCommand(
    string Content,
    Guid? ParentId = null,
    IReadOnlyList<ImageUpload>? Images = null,
    Guid? QuotedTweetId = null) : IRequest<TweetDto>;
