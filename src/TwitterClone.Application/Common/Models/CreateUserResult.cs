namespace TwitterClone.Application.Common.Models;

/// <summary>
/// Outcome of a user-creation attempt. Keeps Identity's own <c>IdentityResult</c> out of the
/// Application layer — Infrastructure translates failures into plain error messages.
/// </summary>
public record CreateUserResult(bool Succeeded, Guid UserId, IReadOnlyList<string> Errors)
{
    public static CreateUserResult Success(Guid userId) => new(true, userId, []);

    public static CreateUserResult Failure(IEnumerable<string> errors) =>
        new(false, Guid.Empty, errors.ToArray());
}
