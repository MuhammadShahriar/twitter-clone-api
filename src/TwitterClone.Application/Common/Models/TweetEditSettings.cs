namespace TwitterClone.Application.Common.Models;

/// <summary>
/// Options for the tweet-edit feature, bound from the <c>TweetEdit</c> configuration section. The author may
/// only edit a tweet within <see cref="EditWindow"/> of when it was posted (Twitter-style restriction).
/// Registered as a configured singleton in Infrastructure, so the Application layer takes a dependency on a
/// plain POCO (no <c>IOptions</c>/configuration package leaking in).
/// </summary>
public class TweetEditSettings
{
    public const string SectionName = "TweetEdit";

    /// <summary>How long after posting a tweet stays editable, in minutes. Default 30.</summary>
    public int WindowMinutes { get; set; } = 30;

    /// <summary>The edit window as a <see cref="TimeSpan"/>.</summary>
    public TimeSpan EditWindow => TimeSpan.FromMinutes(WindowMinutes);
}
