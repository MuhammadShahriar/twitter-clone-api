namespace TwitterClone.Api.Models;

/// <summary>JSON body for <c>POST /api/conversations/{id}/messages</c> — the message text.</summary>
public class SendMessageRequest
{
    /// <summary>The message text (non-empty, length-capped).</summary>
    public string? Content { get; set; }
}
