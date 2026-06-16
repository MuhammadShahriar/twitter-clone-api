namespace TwitterClone.Domain.Common;

/// <summary>
/// Base type for all domain entities. Keeps identity and audit concerns in one place.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
