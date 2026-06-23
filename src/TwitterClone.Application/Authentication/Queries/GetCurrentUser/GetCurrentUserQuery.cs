using MediatR;

namespace TwitterClone.Application.Authentication.Queries.GetCurrentUser;

/// <summary>Resolves the caller from <c>ICurrentUserService</c> and returns their current profile.</summary>
public record GetCurrentUserQuery : IRequest<CurrentUserDto?>;
