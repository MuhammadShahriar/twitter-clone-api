using TwitterClone.Domain.Entities;

namespace TwitterClone.Application.Tweets;

/// <summary>Read model returned by the API for a tweet.</summary>
public record TweetDto(Guid Id, string Content, string AuthorHandle, DateTime CreatedAtUtc)
{
    public static TweetDto FromEntity(Tweet tweet) =>
        new(tweet.Id, tweet.Content, tweet.AuthorHandle, tweet.CreatedAtUtc);
}
