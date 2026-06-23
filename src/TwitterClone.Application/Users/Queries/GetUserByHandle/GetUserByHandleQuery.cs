using MediatR;

namespace TwitterClone.Application.Users.Queries.GetUserByHandle;

/// <summary>
/// Fetches the lite public profile for a user by handle (with follower/following counts and the caller's
/// "followed by me" flag), or <c>null</c> if no such user exists. Public — the flag reflects the caller.
/// </summary>
public record GetUserByHandleQuery(string Handle) : IRequest<UserDto?>;
