namespace TwitterClone.Application.Tweets;

/// <summary>
/// Identifies the user who retweeted a tweet into a timeline. Set on <see cref="TweetDto.RetweetedBy"/>
/// only for retweet entries in the <b>Following feed</b> (the followed user whose retweet surfaced the
/// tweet); it is <c>null</c> for an original tweet and for every other read.
/// </summary>
public record RetweetedByDto(Guid UserId, string Handle, string DisplayName);
