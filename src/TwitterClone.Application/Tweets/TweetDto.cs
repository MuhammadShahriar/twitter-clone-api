namespace TwitterClone.Application.Tweets;

/// <summary>
/// Read model returned by the API for a tweet. The author fields are populated by a read-side join
/// to the user table (done in Infrastructure), so the Application layer never sees the Identity type.
/// <para>
/// <see cref="ParentId"/> is the tweet this one replies to (null for a top-level tweet); <see cref="ReplyCount"/>
/// is the number of direct replies, computed on the read side (a correlated count — no denormalised counter).
/// </para>
/// <para>
/// <see cref="LikeCount"/>/<see cref="RetweetCount"/> are the engagement totals (also correlated counts);
/// <see cref="LikedByCurrentUser"/>/<see cref="RetweetedByCurrentUser"/> say whether the caller has liked /
/// retweeted this tweet (always <c>false</c> for an anonymous reader). <see cref="Media"/> are the attached
/// images (hosted URL + order), populated by the read-side projection.
/// </para>
/// <para>
/// <see cref="RetweetedBy"/> is non-null only for a retweet entry in the <b>Following feed</b> — the
/// followed user whose retweet surfaced the tweet. It is <c>null</c> for an original tweet and for every
/// other read.
/// </para>
/// </summary>
public record TweetDto(
    Guid Id,
    string Content,
    Guid AuthorId,
    string AuthorHandle,
    string AuthorDisplayName,
    DateTime CreatedAtUtc,
    Guid? ParentId,
    int ReplyCount,
    int LikeCount,
    int RetweetCount,
    bool LikedByCurrentUser,
    bool RetweetedByCurrentUser,
    RetweetedByDto? RetweetedBy,
    IReadOnlyList<TweetMediaDto> Media);
