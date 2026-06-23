using MediatR;

namespace TwitterClone.Application.Tweets.Commands.DeleteTweet;

/// <summary>
/// Deletes a tweet (and its direct replies). Only the author may delete their own tweet — the handler
/// enforces this from the authenticated caller, never the request body. Returns nothing; the API maps a
/// success to <c>204</c>, a missing tweet to <c>404</c>, and someone else's tweet to <c>403</c>.
/// </summary>
public record DeleteTweetCommand(Guid Id) : IRequest;
