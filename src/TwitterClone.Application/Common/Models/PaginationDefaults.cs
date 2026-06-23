namespace TwitterClone.Application.Common.Models;

/// <summary>Shared page-size limits for cursor-paginated reads (feed + replies).</summary>
public static class PaginationDefaults
{
    /// <summary>Page size used when the caller does not specify one.</summary>
    public const int DefaultLimit = 20;

    /// <summary>Upper bound on the page size a caller may request.</summary>
    public const int MaxLimit = 50;

    /// <summary>Clamps a requested limit into <c>[1, <see cref="MaxLimit"/>]</c>, defaulting when null.</summary>
    public static int Clamp(int? requested) =>
        Math.Clamp(requested ?? DefaultLimit, 1, MaxLimit);
}
