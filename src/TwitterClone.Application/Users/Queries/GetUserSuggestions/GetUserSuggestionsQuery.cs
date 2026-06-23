using MediatR;

namespace TwitterClone.Application.Users.Queries.GetUserSuggestions;

/// <summary>
/// "Who to follow" suggestions for the authenticated caller: a short list of users they don't already
/// follow, most-followed first. <see cref="Limit"/> is clamped to <c>[1, <see cref="MaxLimit"/>]</c>,
/// defaulting to <see cref="DefaultLimit"/>. Requires an authenticated caller (the API maps a missing
/// token to <c>401</c>).
/// </summary>
public record GetUserSuggestionsQuery(int? Limit = null) : IRequest<IReadOnlyList<UserSuggestionDto>>
{
    /// <summary>Number of suggestions returned when the caller does not specify one.</summary>
    public const int DefaultLimit = 5;

    /// <summary>Upper bound on the number of suggestions a caller may request.</summary>
    public const int MaxLimit = 10;
}
