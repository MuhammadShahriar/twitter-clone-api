using MediatR;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Tweets.Queries.GetTweets;

/// <summary>Lists tweets, newest first.</summary>
public record GetTweetsQuery : IRequest<IReadOnlyList<TweetDto>>;
