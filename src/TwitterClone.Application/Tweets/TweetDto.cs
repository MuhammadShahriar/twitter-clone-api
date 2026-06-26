namespace TwitterClone.Application.Tweets;

/// <summary>
/// Read model returned by the API for a tweet. The author fields (handle, display name, avatar) are
/// populated by a read-side join to the user table (done in Infrastructure), so the Application layer never
/// sees the Identity type. <see cref="AuthorAvatarUrl"/> is the tweet author's avatar (null ⇒ no avatar set).
/// <para>
/// <see cref="ParentId"/> is the tweet this one replies to (null for a top-level tweet); <see cref="ReplyCount"/>
/// is the number of direct replies, computed on the read side (a correlated count — no denormalised counter).
/// </para>
/// <para>
/// <see cref="LikeCount"/>/<see cref="RetweetCount"/> are the engagement totals (also correlated counts);
/// <see cref="LikedByCurrentUser"/>/<see cref="RetweetedByCurrentUser"/> say whether the caller has liked /
/// retweeted this tweet (always <c>false</c> for an anonymous reader). <see cref="BookmarkedByCurrentUser"/>
/// says whether the caller has bookmarked it (also <c>false</c> when anonymous); bookmarks are private, so
/// there is no public bookmark <em>count</em>. <see cref="Media"/> are the attached images (hosted URL +
/// order), populated by the read-side projection.
/// </para>
/// <para>
/// <see cref="RetweetedBy"/> is non-null only for a retweet entry in the <b>Following feed</b> — the
/// followed user whose retweet surfaced the tweet. It is <c>null</c> for an original tweet and for every
/// other read.
/// </para>
/// <para>
/// <see cref="QuoteCount"/> is the number of tweets that quote this one (a correlated count, like the
/// others); <see cref="QuotedTweet"/> is a one-level preview of the tweet this one quotes — non-null only
/// for a quote tweet, and <c>null</c> when the quoted tweet has since been deleted ("unavailable").
/// <see cref="IsQuote"/> is the delete-surviving flag that distinguishes a non-quote (<c>false</c> — the
/// client shows nothing) from a quote whose target was deleted (<c>true</c> with a <c>null</c>
/// <see cref="QuotedTweet"/> — the client shows "This post is unavailable"), since the quote self-FK is
/// <c>SET NULL</c> and so <c>QuotedTweet</c> alone is ambiguous.
/// </para>
/// <para>
/// <see cref="EditedAtUtc"/> is when the tweet was last edited (<c>null</c> until first edited) — the UI
/// shows an "edited" marker when it is set. <see cref="CreatedAtUtc"/> is never changed by an edit.
/// </para>
/// </summary>
public record TweetDto(
    Guid Id,
    string Content,
    Guid AuthorId,
    string AuthorHandle,
    string AuthorDisplayName,
    string? AuthorAvatarUrl,
    DateTime CreatedAtUtc,
    Guid? ParentId,
    int ReplyCount,
    int LikeCount,
    int RetweetCount,
    bool LikedByCurrentUser,
    bool RetweetedByCurrentUser,
    bool BookmarkedByCurrentUser,
    RetweetedByDto? RetweetedBy,
    IReadOnlyList<TweetMediaDto> Media,
    int QuoteCount,
    bool IsQuote,
    QuotedTweetDto? QuotedTweet,
    DateTime? EditedAtUtc);
