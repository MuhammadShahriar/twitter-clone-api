namespace TwitterClone.Application.Common.Exceptions;

/// <summary>
/// Provider-agnostic signal that a write lost a race to a unique constraint (a concurrent insert of the
/// same row committed first). Raised by the Unit of Work so the Application layer can react without
/// referencing EF Core / Npgsql. Engagement and follow handlers catch this and treat it as the idempotent
/// success it logically is (the row now exists either way). If it escapes a handler, it is an unexpected
/// conflict — there is deliberately no HTTP mapping, so it surfaces as a 500.
/// </summary>
public class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
