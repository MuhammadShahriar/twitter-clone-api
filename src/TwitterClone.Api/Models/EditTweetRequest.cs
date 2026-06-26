namespace TwitterClone.Api.Models;

/// <summary>
/// JSON body for <c>PUT /api/tweets/{id}</c> — edit a tweet's text. Text only (v1); media is not editable.
/// </summary>
public class EditTweetRequest
{
    /// <summary>The new text for the tweet.</summary>
    public string? Content { get; set; }
}
