namespace TwitterClone.Application.Common.Interfaces;

/// <summary>
/// Exposes the caller behind the current request to the Application layer. Implemented in the API layer
/// by reading the validated JWT principal — but this abstraction is deliberately HTTP/JWT/Identity-free
/// so handlers can ask "who is calling?" without depending on any of that machinery.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>The authenticated user's id (from the token's <c>sub</c> claim), or <c>null</c> if anonymous.</summary>
    Guid? UserId { get; }

    /// <summary>The authenticated user's handle (from the token), or <c>null</c> if anonymous.</summary>
    string? Handle { get; }

    /// <summary>True when the request carries a valid authenticated principal.</summary>
    bool IsAuthenticated { get; }
}
