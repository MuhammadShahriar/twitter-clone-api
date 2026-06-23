using MediatR;

namespace TwitterClone.Application.Users.Commands.FollowUser;

/// <summary>
/// Follows the user with the given handle on behalf of the authenticated caller (the follower is taken
/// from the token, never the body). <b>Idempotent</b>: following an already-followed user is a no-op
/// success. Returns the followee's updated lite profile. The API maps a missing handle to <c>404</c>, a
/// self-follow to <c>400</c>, and a missing token to <c>401</c>.
/// </summary>
public record FollowUserCommand(string Handle) : IRequest<UserDto>;
