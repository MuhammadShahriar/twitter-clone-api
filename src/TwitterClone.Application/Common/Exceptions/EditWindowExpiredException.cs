namespace TwitterClone.Application.Common.Exceptions;

/// <summary>
/// Thrown when the author tries to edit their tweet after the edit window has closed. The caller IS the
/// author (so it isn't a 403 authorization failure) — the tweet's age simply conflicts with the edit, so the
/// API maps this to <c>409 Conflict</c>. The message states the window plainly.
/// </summary>
public class EditWindowExpiredException : Exception
{
    public EditWindowExpiredException(TimeSpan window)
        : base($"This tweet can no longer be edited — edits are only allowed within {window.TotalMinutes:0} minutes of posting.")
    {
    }
}
