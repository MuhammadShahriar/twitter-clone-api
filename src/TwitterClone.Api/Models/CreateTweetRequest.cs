using Microsoft.AspNetCore.Mvc;

namespace TwitterClone.Api.Models;

/// <summary>
/// Multipart form model for <c>POST /api/tweets</c>. Bound from <c>multipart/form-data</c> so a tweet can be
/// created together with its image files in a single request. The controller maps this to a
/// <c>CreateTweetCommand</c> (reading the files into provider-free models) — the Application never sees
/// <see cref="IFormFile"/>.
/// </summary>
public class CreateTweetRequest
{
    /// <summary>The tweet's text.</summary>
    [FromForm(Name = "content")]
    public string? Content { get; set; }

    /// <summary>When set, the id of the tweet this one replies to.</summary>
    [FromForm(Name = "parentId")]
    public Guid? ParentId { get; set; }

    /// <summary>The image files to attach (field name <c>images</c>); up to four, validated downstream.</summary>
    [FromForm(Name = "images")]
    public IFormFileCollection? Images { get; set; }
}
