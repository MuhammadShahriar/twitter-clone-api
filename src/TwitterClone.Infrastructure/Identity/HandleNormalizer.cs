namespace TwitterClone.Infrastructure.Identity;

/// <summary>
/// Canonicalises a user @handle for case-insensitive, @-tolerant matching. The display
/// <see cref="ApplicationUser.Handle"/> is stored verbatim (case + any leading @ preserved); the
/// <see cref="ApplicationUser.NormalizedHandle"/> column stores <see cref="Normalize"/>'s output, which
/// carries the unique index and is what every lookup compares against — so <c>@Ada</c>, <c>ada</c> and
/// <c>@ada</c> all resolve to the same account and can't be registered twice. Mirrors how Identity keeps
/// a NormalizedUserName/NormalizedEmail alongside the display value.
/// </summary>
public static class HandleNormalizer
{
    /// <summary>
    /// Trims whitespace, drops a single optional leading <c>@</c>, and upper-cases (invariant) — the
    /// canonical form stored in <c>NormalizedHandle</c> and used for all handle comparisons.
    /// </summary>
    public static string Normalize(string? handle)
    {
        var trimmed = (handle ?? string.Empty).Trim();
        if (trimmed.StartsWith('@'))
        {
            trimmed = trimmed[1..];
        }

        return trimmed.ToUpperInvariant();
    }
}
