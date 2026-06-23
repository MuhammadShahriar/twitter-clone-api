namespace TwitterClone.Application.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated caller tries to act on a resource they do not own (e.g. deleting another
/// user's tweet). The API maps this to <c>403 Forbidden</c> — distinct from a <c>404</c>, so the caller
/// learns the resource exists but is not theirs to touch. The message is deliberately generic.
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException()
        : base("You do not have permission to perform this action.")
    {
    }
}
