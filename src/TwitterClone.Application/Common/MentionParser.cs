using System.Text.RegularExpressions;

namespace TwitterClone.Application.Common;

/// <summary>
/// Extracts the <c>@handle</c> mentions from a tweet's text. Pure text logic (no Identity, no DB) so it
/// lives in Application and is reused by the create-tweet handler. The raw tokens it returns are resolved
/// to user ids by <see cref="Interfaces.IUserRepository.GetIdsByHandlesAsync"/> (which normalises and
/// matches against <c>NormalizedHandle</c>) — so the parser stays agnostic of how handles are canonicalised.
/// </summary>
public static partial class MentionParser
{
    // A mention is '@' followed by handle characters (letters/digits/underscore — the set the register
    // validator allows). The leading negative lookbehind '(?<![A-Za-z0-9_])' requires the '@' NOT be preceded
    // by a word character, so an email address ("alice@example.com") does not register a "@example" mention.
    [GeneratedRegex(@"(?<![A-Za-z0-9_])@([A-Za-z0-9_]+)")]
    private static partial Regex MentionRegex();

    /// <summary>
    /// The distinct handles mentioned in <paramref name="content"/> (without the leading <c>@</c>), in order
    /// of first appearance. Distinctness is case-insensitive, so <c>@alice @Alice</c> yields a single token.
    /// </summary>
    public static IReadOnlyList<string> ExtractHandles(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        var handles = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in MentionRegex().Matches(content))
        {
            var handle = match.Groups[1].Value;
            if (seen.Add(handle))
            {
                handles.Add(handle);
            }
        }

        return handles;
    }
}
