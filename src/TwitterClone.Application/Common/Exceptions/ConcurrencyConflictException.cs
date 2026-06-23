namespace TwitterClone.Application.Common.Exceptions;

/// <summary>
/// Provider-agnostic signal that a write found no row to affect because a concurrent write already changed
/// it (an optimistic-concurrency / "0 rows affected" conflict — e.g. a concurrent delete removed the row
/// first). Raised by the Unit of Work so the Application layer can react without referencing EF Core.
/// Unlike/unretweet/unfollow handlers catch this and treat it as the idempotent success it logically is
/// (the row is gone either way). If it escapes a handler, it surfaces as a 500 (no HTTP mapping).
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
