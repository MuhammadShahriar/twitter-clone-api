using MediatR;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Bookmarks.Queries.GetBookmarks;

/// <summary>
/// The authenticated caller's own bookmarks ("Bookmarks" page): the tweets they have bookmarked,
/// most-recently-bookmarked first, cursor-paginated. <b>Private</b> — there is no handle/owner parameter; the
/// owner is taken from the token by the handler, so a user only ever sees their own bookmarks (and the
/// per-tweet by-me flags reflect them). <see cref="Cursor"/> is the opaque token from a previous page's
/// <c>nextCursor</c>; <see cref="Limit"/> is clamped to a sane range by the handler.
/// </summary>
public record GetBookmarksQuery(string? Cursor = null, int? Limit = null)
    : IRequest<CursorPage<TweetDto>>;
