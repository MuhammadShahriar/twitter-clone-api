using MediatR;

namespace TwitterClone.Application.Users.Commands.UnfollowUser;

/// <summary>
/// Removes the authenticated caller's follow of the user with the given handle. <b>Idempotent</b>:
/// unfollowing a user the caller does not follow is a no-op success. Returns the (former) followee's
/// updated lite profile. The API maps a missing handle to <c>404</c> and a missing token to <c>401</c>.
/// </summary>
public record UnfollowUserCommand(string Handle) : IRequest<UserDto>;
