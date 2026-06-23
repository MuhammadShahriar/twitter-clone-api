using MediatR;
using TwitterClone.Application.Common.Interfaces;

namespace TwitterClone.Application.Users.Queries.GetUserSuggestions;

public class GetUserSuggestionsQueryHandler(IUserRepository userRepository, ICurrentUserService currentUser)
    : IRequestHandler<GetUserSuggestionsQuery, IReadOnlyList<UserSuggestionDto>>
{
    public async Task<IReadOnlyList<UserSuggestionDto>> Handle(
        GetUserSuggestionsQuery request, CancellationToken cancellationToken)
    {
        // The controller's [Authorize] guarantees a user; this guard is defensive (suggestions are personal).
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Follow suggestions require an authenticated user.");

        var limit = Math.Clamp(
            request.Limit ?? GetUserSuggestionsQuery.DefaultLimit, 1, GetUserSuggestionsQuery.MaxLimit);

        return await userRepository.GetSuggestionsAsync(userId, limit, cancellationToken);
    }
}
