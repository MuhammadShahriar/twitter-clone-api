namespace TwitterClone.Application.Tweets;

/// <summary>
/// A lightweight, <b>one-level</b> preview of the tweet that a quote tweet embeds — just enough to render an
/// embedded card. Deliberately <b>non-recursive</b>: it carries no <c>QuotedTweet</c>/<c>Parent</c>/counts of
/// its own, so a quote-of-a-quote previews only a single level (no deep or infinite nesting). Set on
/// <see cref="TweetDto.QuotedTweet"/>; <c>null</c> when the tweet isn't a quote or the quoted tweet has been
/// deleted (then the client shows "This post is unavailable.").
/// </summary>
public record QuotedTweetDto(
    Guid Id,
    string Content,
    QuotedTweetAuthorDto Author,
    DateTime CreatedAtUtc,
    IReadOnlyList<TweetMediaDto> Media);

/// <summary>The author fields shown on an embedded quoted-tweet card (handle, display name, avatar).</summary>
public record QuotedTweetAuthorDto(string Handle, string DisplayName, string? AvatarUrl);
