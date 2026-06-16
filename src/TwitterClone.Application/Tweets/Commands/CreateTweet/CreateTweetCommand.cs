using MediatR;
using TwitterClone.Application.Tweets;

namespace TwitterClone.Application.Tweets.Commands.CreateTweet;

/// <summary>Creates a new tweet and returns the persisted read model.</summary>
public record CreateTweetCommand(string Content, string AuthorHandle) : IRequest<TweetDto>;
