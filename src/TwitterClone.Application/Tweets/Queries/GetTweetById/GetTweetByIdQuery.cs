using MediatR;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Tweets.Queries.GetTweetById;

/// <summary>Fetches a single tweet by id, or <c>null</c> if it does not exist.</summary>
public record GetTweetByIdQuery(Guid Id) : IRequest<TweetDto?>;
