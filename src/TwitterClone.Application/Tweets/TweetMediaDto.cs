namespace TwitterClone.Application.Tweets;

/// <summary>Read model for an image attached to a tweet: its hosted URL, storage public id, and order.</summary>
public record TweetMediaDto(string Url, string PublicId, int Position);
