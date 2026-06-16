using TwitterClone.Domain.Common;

namespace TwitterClone.Domain.Entities;

/// <summary>
/// A single tweet. The only aggregate in the Module 0 walking skeleton.
/// </summary>
public class Tweet : BaseEntity
{
    /// <summary>Maximum length of a tweet's text, matching Twitter's classic limit.</summary>
    public const int MaxContentLength = 280;

    /// <summary>Maximum length of an author handle.</summary>
    public const int MaxAuthorHandleLength = 50;

    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The handle of the author. There is no user/auth concept yet in Module 0,
    /// so this is a free-form string captured at creation time.
    /// </summary>
    public string AuthorHandle { get; set; } = string.Empty;
}
