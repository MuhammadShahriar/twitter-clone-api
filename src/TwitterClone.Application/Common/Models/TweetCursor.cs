using System.Buffers.Text;
using System.Text;

namespace TwitterClone.Application.Common.Models;

/// <summary>
/// The position of the last item on a page, used to fetch the next one. Tweets are ordered by
/// <see cref="CreatedAtUtc"/> with <see cref="Id"/> as a stable tiebreaker, so the cursor carries
/// both — paging never duplicates or skips rows that share a timestamp.
/// </summary>
public sealed record TweetCursor(DateTime CreatedAtUtc, Guid Id)
{
    /// <summary>
    /// Encodes the cursor as an opaque, URL-safe base64 token (<c>{ticks}:{id}</c>). Clients treat it as
    /// a black box — the shape is an implementation detail and may change.
    /// </summary>
    public string Encode()
    {
        var payload = $"{CreatedAtUtc.Ticks}:{Id}";
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>
    /// Decodes a token produced by <see cref="Encode"/>. Returns <c>null</c> for a null/empty or
    /// malformed token, so a bad cursor is treated leniently as "start from the beginning".
    /// </summary>
    public static TweetCursor? Decode(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
            var separator = payload.IndexOf(':');
            if (separator <= 0)
            {
                return null;
            }

            if (!long.TryParse(payload[..separator], out var ticks)
                || !Guid.TryParse(payload[(separator + 1)..], out var id))
            {
                return null;
            }

            return new TweetCursor(new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
