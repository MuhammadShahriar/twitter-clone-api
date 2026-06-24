using Microsoft.AspNetCore.Mvc;

namespace TwitterClone.Api.Models;

/// <summary>
/// Multipart form model for <c>POST /api/users/me/avatar</c>. Bound from <c>multipart/form-data</c> so the
/// avatar image is uploaded directly. The controller maps the file to a provider-free <c>ImageUpload</c> —
/// the Application never sees <see cref="IFormFile"/>.
/// </summary>
public class UpdateAvatarRequest
{
    /// <summary>The avatar image file (field name <c>image</c>); size/type validated downstream.</summary>
    [FromForm(Name = "image")]
    public IFormFile? Image { get; set; }
}
