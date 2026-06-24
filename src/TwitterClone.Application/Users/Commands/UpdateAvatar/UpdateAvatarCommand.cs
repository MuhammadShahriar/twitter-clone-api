using MediatR;
using TwitterClone.Application.Common.Models;

namespace TwitterClone.Application.Users.Commands.UpdateAvatar;

/// <summary>
/// Sets the authenticated caller's avatar from an uploaded image (backend-proxied to the image host) and
/// returns their refreshed lite profile. The user is taken from the token; the API layer reads the multipart
/// file into the provider-free <see cref="ImageUpload"/> so the Application never sees <c>IFormFile</c>. The
/// validator caps the image size/type before any upload happens.
/// </summary>
public record UpdateAvatarCommand(ImageUpload Image) : IRequest<UserDto>;
