using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Tweet-specific repository. Entity-specific queries live here as named methods
/// (rather than exposing <c>IQueryable</c>), keeping the abstraction clean.
///
/// The read methods project straight into <see cref="TweetDto"/>, joining each tweet to its author in
/// Infrastructure so the author handle/display name come back without the Identity type ever crossing
/// into Application. Reads are cursor-paginated (see <see cref="TweetCursor"/>) and carry computed
/// <c>ReplyCount</c>/<c>LikeCount</c>/<c>RetweetCount</c> totals plus the caller's like/retweet flags —
/// hence <paramref name="currentUserId"/> on each read (<c>null</c> for an anonymous reader: the flags
/// then come back <c>false</c>).
/// </summary>
public interface ITweetRepository : IRepository<Tweet>
{
    /// <summary>
    /// The main feed: <b>top-level tweets only</b> (<c>ParentId == null</c>), newest-first
    /// (by <c>CreatedAtUtc</c> then <c>Id</c> descending), one cursor-paginated page at a time.
    /// </summary>
    Task<CursorPage<TweetDto>> GetFeedAsync(Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>
    /// The direct replies to <paramref name="parentId"/>, <b>oldest-first</b> (by <c>CreatedAtUtc</c> then
    /// <c>Id</c> ascending — natural thread reading order), one cursor-paginated page at a time.
    /// </summary>
    Task<CursorPage<TweetDto>> GetRepliesAsync(Guid parentId, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>
    /// The <b>Following feed</b> for <paramref name="currentUserId"/>: a merged, newest-first timeline of
    /// top-level tweets <b>authored</b> by the users they follow plus tweets those users <b>retweeted</b>
    /// (each ordered by its timeline time — the tweet's creation time, or the retweet's time for a retweet).
    /// A retweet entry carries <c>RetweetedBy</c>; an authored entry does not. Cursor-paginated like the feed.
    /// </summary>
    Task<CursorPage<TweetDto>> GetFollowingFeedAsync(Guid currentUserId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>
    /// A user's own timeline: the <b>top-level tweets they authored</b> (<c>ParentId == null</c>),
    /// newest-first, cursor-paginated like the main feed. <paramref name="currentUserId"/> drives the
    /// caller's like/retweet flags (<c>null</c> ⇒ false for an anonymous reader).
    /// </summary>
    Task<CursorPage<TweetDto>> GetUserTweetsAsync(Guid authorId, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>
    /// The tweets a user has <b>liked</b>, most-recently-liked first (keyset on the like's time then tweet id),
    /// cursor-paginated. <paramref name="currentUserId"/> drives the caller's like/retweet flags on each tweet.
    /// </summary>
    Task<CursorPage<TweetDto>> GetUserLikedTweetsAsync(Guid likerId, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default);

    /// <summary>Fetches a single tweet (with author info, counts, and the caller's flags) by id, or <c>null</c> if it does not exist.</summary>
    Task<TweetDto?> GetByIdWithAuthorAsync(Guid id, Guid? currentUserId, CancellationToken ct = default);

    /// <summary>True if a tweet with the given id exists (used to validate a reply's parent).</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The id of the tweet's author (their <c>AspNetUsers.Id</c>), or <c>null</c> if the tweet doesn't
    /// exist. Used to resolve the notification recipient when someone acts on a tweet, without fetching
    /// the whole row.
    /// </summary>
    Task<Guid?> GetAuthorIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Loads the direct replies to <paramref name="parentId"/> as tracked entities so they can be staged for
    /// removal when their parent is deleted (cascade is handled in the delete handler for provider-independence).
    /// </summary>
    Task<IReadOnlyList<Tweet>> GetDirectRepliesAsync(Guid parentId, CancellationToken ct = default);
}
