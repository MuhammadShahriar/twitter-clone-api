namespace TwitterClone.Api.Models;

/// <summary>
/// JSON body for <c>POST /api/conversations</c> — identify the recipient by handle or by user id (one of the
/// two). The caller is taken from the token.
/// </summary>
public class StartConversationRequest
{
    /// <summary>The recipient's @handle (case-insensitive, leading @ tolerated).</summary>
    public string? RecipientHandle { get; set; }

    /// <summary>The recipient's user id (e.g. from a profile). Used when no handle is supplied.</summary>
    public Guid? RecipientUserId { get; set; }
}
