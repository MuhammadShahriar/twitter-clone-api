using MediatR;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Tweets.Queries.GetReplies;

/// <summary>
/// The direct replies to <see cref="ParentId"/>, oldest-first (natural thread order), cursor-paginated.
/// <see cref="Cursor"/>/<see cref="Limit"/> behave as on the feed query.
/// </summary>
public record GetRepliesQuery(Guid ParentId, string? Cursor = null, int? Limit = null)
    : IRequest<CursorPage<TweetDto>>;
