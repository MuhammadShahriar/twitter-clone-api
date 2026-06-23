namespace TwitterClone.Application.Common.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist. The API maps this to <c>404 Not Found</c>.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string name, object key)
        : base($"{name} ({key}) was not found.")
    {
    }
}
