using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Infrastructure.Persistence.Repositories;

/// <summary>
/// Tweet-specific repository. Inherits the generic staging operations and adds the read queries, which
/// join each tweet to its author in <c>AspNetUsers</c> and project straight into <see cref="TweetDto"/>.
/// The join lives here (Infrastructure) so the Identity type never leaks into Application; ordering and
/// filtering are pushed down to the database, reads stay untracked, and the reply count is a correlated
/// subquery (no N+1). Feed/reply reads are cursor-paginated for stable, infinite-scroll-friendly paging.
/// </summary>
public class TweetRepository(ApplicationDbContext context)
    : Repository<Tweet>(context), ITweetRepository
{
    public async Task<CursorPage<TweetDto>> GetFeedAsync(
        Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default)
    {
        var position = TweetCursor.Decode(cursor);

        // Feed = top-level tweets only; newest-first. The cursor predicate is "strictly older than the last
        // item seen", with Id as a tiebreaker so rows sharing a CreatedAtUtc page deterministically.
        var query = Context.Tweets.AsNoTracking().Where(t => t.ParentId == null);
        if (position is not null)
        {
            query = query.Where(t =>
                t.CreatedAtUtc < position.CreatedAtUtc
                || (t.CreatedAtUtc == position.CreatedAtUtc && t.Id.CompareTo(position.Id) < 0));
        }

        var rows = await Project(query
                .OrderByDescending(t => t.CreatedAtUtc)
                .ThenByDescending(t => t.Id), currentUserId)
            .Take(limit + 1)
            .ToListAsync(ct);

        return ToPage(rows, limit);
    }

    public async Task<CursorPage<TweetDto>> GetRepliesAsync(
        Guid parentId, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default)
    {
        var position = TweetCursor.Decode(cursor);

        // Direct replies to one tweet; oldest-first so a thread reads top-to-bottom in chronological order.
        var query = Context.Tweets.AsNoTracking().Where(t => t.ParentId == parentId);
        if (position is not null)
        {
            query = query.Where(t =>
                t.CreatedAtUtc > position.CreatedAtUtc
                || (t.CreatedAtUtc == position.CreatedAtUtc && t.Id.CompareTo(position.Id) > 0));
        }

        var rows = await Project(query
                .OrderBy(t => t.CreatedAtUtc)
                .ThenBy(t => t.Id), currentUserId)
            .Take(limit + 1)
            .ToListAsync(ct);

        return ToPage(rows, limit);
    }

    public async Task<CursorPage<TweetDto>> GetFollowingFeedAsync(
        Guid currentUserId, string? cursor, int limit, CancellationToken ct = default)
    {
        var position = TweetCursor.Decode(cursor);

        var followeeIds = Context.Follows
            .Where(f => f.FollowerId == currentUserId)
            .Select(f => f.FolloweeId);

        // Every way a tweet can surface in the feed, each carrying its effective timeline time: a top-level
        // tweet authored by a followee, or a tweet retweeted by a followee.
        var authored = Context.Tweets
            .Where(t => t.ParentId == null && followeeIds.Contains(t.AuthorId))
            .Select(t => new { TweetId = t.Id, SortTime = t.CreatedAtUtc });

        var retweeted = Context.Retweets
            .Where(rt => followeeIds.Contains(rt.UserId))
            .Select(rt => new { TweetId = rt.TweetId, SortTime = rt.CreatedAtUtc });

        // De-duplicate to ONE row per tweet, at its most-recent surfacing time (a popular tweet retweeted by
        // several followees — and/or also authored by one — appears once, not many times). Keyset over
        // (SortTime, TweetId): after the GROUP BY, TweetId is unique per row, so it's a stable tiebreaker.
        var deduped = authored.Concat(retweeted)
            .GroupBy(e => e.TweetId)
            .Select(g => new { TweetId = g.Key, SortTime = g.Max(x => x.SortTime) });

        if (position is not null)
        {
            deduped = deduped.Where(e =>
                e.SortTime < position.CreatedAtUtc
                || (e.SortTime == position.CreatedAtUtc && e.TweetId.CompareTo(position.Id) < 0));
        }

        var pageRows = await deduped
            .OrderByDescending(e => e.SortTime)
            .ThenByDescending(e => e.TweetId)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = pageRows.Count > limit;
        var page = hasMore ? pageRows.Take(limit).ToList() : pageRows;
        var tweetIds = page.Select(e => e.TweetId).ToList();

        // Load the tweet projections (with the caller's like/retweet flags) for the page in one query.
        var tweetsById = await Project(
                Context.Tweets.AsNoTracking().Where(t => tweetIds.Contains(t.Id)), currentUserId)
            .ToDictionaryAsync(t => t.Id, ct);

        // The latest followee-retweet of each page tweet — used to decide whether the most-recent surfacing
        // is a retweet (and by whom) or the authored original. One query for the whole page (no N+1).
        var pageRetweets = await Context.Retweets
            .Where(rt => followeeIds.Contains(rt.UserId) && tweetIds.Contains(rt.TweetId))
            .Select(rt => new { rt.TweetId, rt.UserId, rt.CreatedAtUtc })
            .ToListAsync(ct);

        var latestRetweetByTweet = pageRetweets
            .GroupBy(rt => rt.TweetId)
            .ToDictionary(
                g => g.Key,
                // Deterministic tiebreak (UserId) if two followees retweeted at the same instant.
                g => g.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.UserId).First());

        var retweeterIds = latestRetweetByTweet.Values.Select(x => x.UserId).Distinct().ToList();
        var retweetersById = retweeterIds.Count == 0
            ? new Dictionary<Guid, RetweetedByDto>()
            : await Context.Users
                .Where(u => retweeterIds.Contains(u.Id))
                .Select(u => new RetweetedByDto(u.Id, u.Handle, u.DisplayName))
                .ToDictionaryAsync(u => u.UserId, ct);

        // Stitch back in timeline order. The most-recent surfacing is a retweet iff a followee retweeted this
        // tweet at the row's effective time (ties between an original and a retweet go to the retweet); else
        // it's the authored original (RetweetedBy stays null). A tweet deleted in between is simply skipped.
        var items = new List<TweetDto>(page.Count);
        foreach (var row in page)
        {
            if (!tweetsById.TryGetValue(row.TweetId, out var dto))
            {
                continue;
            }

            if (latestRetweetByTweet.TryGetValue(row.TweetId, out var rt)
                && rt.CreatedAtUtc == row.SortTime
                && retweetersById.TryGetValue(rt.UserId, out var by))
            {
                dto = dto with { RetweetedBy = by };
            }

            items.Add(dto);
        }

        var nextCursor = hasMore && page.Count > 0
            ? new TweetCursor(page[^1].SortTime, page[^1].TweetId).Encode()
            : null;

        return new CursorPage<TweetDto>(items, nextCursor);
    }

    public async Task<CursorPage<TweetDto>> GetUserTweetsAsync(
        Guid authorId, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default)
    {
        var position = TweetCursor.Decode(cursor);

        // A profile's "Tweets" tab: top-level tweets the user authored, newest-first (same keyset as the
        // main feed — (CreatedAtUtc, Id) descending, Id the stable tiebreaker).
        var query = Context.Tweets.AsNoTracking()
            .Where(t => t.ParentId == null && t.AuthorId == authorId);
        if (position is not null)
        {
            query = query.Where(t =>
                t.CreatedAtUtc < position.CreatedAtUtc
                || (t.CreatedAtUtc == position.CreatedAtUtc && t.Id.CompareTo(position.Id) < 0));
        }

        var rows = await Project(query
                .OrderByDescending(t => t.CreatedAtUtc)
                .ThenByDescending(t => t.Id), currentUserId)
            .Take(limit + 1)
            .ToListAsync(ct);

        return ToPage(rows, limit);
    }

    public async Task<CursorPage<TweetDto>> GetUserLikedTweetsAsync(
        Guid likerId, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default)
    {
        var position = TweetCursor.Decode(cursor);

        // The "Likes" tab: order by WHEN the user liked the tweet (newest like first), not the tweet's own
        // time — so the keyset is over the like row's (CreatedAtUtc, TweetId). A user likes a tweet at most
        // once, so TweetId is unique within this set and serves as the stable tiebreaker. We page the like
        // rows first, then load the tweet projections for just that page (one query) and stitch them back in
        // like-order — mirroring how the Following feed pages a derived ordering it can't project through.
        var likes = Context.Likes.AsNoTracking().Where(l => l.UserId == likerId);
        if (position is not null)
        {
            likes = likes.Where(l =>
                l.CreatedAtUtc < position.CreatedAtUtc
                || (l.CreatedAtUtc == position.CreatedAtUtc && l.TweetId.CompareTo(position.Id) < 0));
        }

        var pageLikes = await likes
            .OrderByDescending(l => l.CreatedAtUtc)
            .ThenByDescending(l => l.TweetId)
            .Take(limit + 1)
            .Select(l => new { l.TweetId, l.CreatedAtUtc })
            .ToListAsync(ct);

        var hasMore = pageLikes.Count > limit;
        var page = hasMore ? pageLikes.Take(limit).ToList() : pageLikes;
        var tweetIds = page.Select(l => l.TweetId).ToList();

        var tweetsById = await Project(
                Context.Tweets.AsNoTracking().Where(t => tweetIds.Contains(t.Id)), currentUserId)
            .ToDictionaryAsync(t => t.Id, ct);

        // Re-stitch in like-order; a tweet liked but since deleted is simply skipped.
        var items = new List<TweetDto>(page.Count);
        foreach (var like in page)
        {
            if (tweetsById.TryGetValue(like.TweetId, out var dto))
            {
                items.Add(dto);
            }
        }

        var nextCursor = hasMore && page.Count > 0
            ? new TweetCursor(page[^1].CreatedAtUtc, page[^1].TweetId).Encode()
            : null;

        return new CursorPage<TweetDto>(items, nextCursor);
    }

    public async Task<TweetDto?> GetByIdWithAuthorAsync(Guid id, Guid? currentUserId, CancellationToken ct = default) =>
        await Project(Context.Tweets.AsNoTracking().Where(t => t.Id == id), currentUserId)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        await Context.Tweets.AsNoTracking().AnyAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Tweet>> GetDirectRepliesAsync(Guid parentId, CancellationToken ct = default) =>
        await Context.Tweets.Where(t => t.ParentId == parentId).ToListAsync(ct);

    /// <summary>
    /// Joins tweets to their author and projects into <see cref="TweetDto"/>, computing the reply / like /
    /// retweet counts as correlated subqueries and the caller's like/retweet flags from
    /// <paramref name="currentUserId"/> (null ⇒ both false — an anonymous reader). Shared by every read so
    /// the projection (and the Identity join) lives in exactly one place.
    /// </summary>
    private IQueryable<TweetDto> Project(IQueryable<Tweet> tweets, Guid? currentUserId) =>
        from tweet in tweets
        join user in Context.Users on tweet.AuthorId equals user.Id
        select new TweetDto(
            tweet.Id,
            tweet.Content,
            tweet.AuthorId,
            user.Handle,
            user.DisplayName,
            // From the already-joined author — no extra query, no N+1. Scope guard: this is the tweet
            // author's avatar only; the retweeter (RetweetedBy) avatar is a separate, deferred enhancement.
            user.AvatarUrl,
            tweet.CreatedAtUtc,
            tweet.ParentId,
            Context.Tweets.Count(r => r.ParentId == tweet.Id),
            Context.Likes.Count(l => l.TweetId == tweet.Id),
            Context.Retweets.Count(r => r.TweetId == tweet.Id),
            currentUserId != null && Context.Likes.Any(l => l.TweetId == tweet.Id && l.UserId == currentUserId),
            currentUserId != null && Context.Retweets.Any(r => r.TweetId == tweet.Id && r.UserId == currentUserId),
            // RetweetedBy is set only by the Following-feed merge (for retweet entries); null for every other read.
            (RetweetedByDto?)null,
            tweet.Media
                .OrderBy(m => m.Position)
                .Select(m => new TweetMediaDto(m.Url, m.PublicId, m.Position))
                .ToList());

    /// <summary>
    /// Turns an over-fetched list (limit + 1) into a page: if the extra row is present there are more
    /// results, so trim it and emit a cursor pointing at the last item actually returned.
    /// </summary>
    private static CursorPage<TweetDto> ToPage(List<TweetDto> rows, int limit)
    {
        if (rows.Count <= limit)
        {
            return new CursorPage<TweetDto>(rows, null);
        }

        var page = rows.Take(limit).ToList();
        var last = page[^1];
        var nextCursor = new TweetCursor(last.CreatedAtUtc, last.Id).Encode();
        return new CursorPage<TweetDto>(page, nextCursor);
    }
}
