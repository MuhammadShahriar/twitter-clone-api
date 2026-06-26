using MediatR;

namespace TwitterClone.Application.Tweets.Commands.EditTweet;

/// <summary>
/// Edits the text of an existing tweet and returns the updated read model. The editor is the authenticated
/// caller (resolved in the handler, never the body); the handler enforces author-only and the edit window.
/// Text only (v1) — media, parent, quoted reference, counts and <c>CreatedAtUtc</c> are untouched, and
/// mentions are not re-parsed or re-notified.
/// </summary>
public record EditTweetCommand(Guid Id, string Content) : IRequest<TweetDto>;
