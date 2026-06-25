using MediatR;

namespace TwitterClone.Application.Users.Commands.DeleteAvatar;

/// <summary>
/// Removes the authenticated caller's avatar (clears the stored URL + public id and best-effort deletes the
/// hosted asset) and returns their refreshed lite profile. The user is taken from the token. Idempotent:
/// removing an avatar when there is none is a no-op success.
/// </summary>
public record DeleteAvatarCommand : IRequest<UserDto>;
