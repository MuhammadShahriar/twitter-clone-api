using System.Text.Json.Serialization;

namespace TwitterClone.Domain.Enums;

/// <summary>
/// The kind of social action that produced a notification. Serialized as its string name on the wire
/// (via the attribute) so API clients read <c>"Like"</c> rather than an opaque integer — the attribute is
/// the single, localized place this enum is made string-friendly, leaving the global JSON config untouched.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationType
{
    /// <summary>Someone liked the recipient's tweet.</summary>
    Like = 0,

    /// <summary>Someone followed the recipient.</summary>
    Follow = 1,

    /// <summary>Someone replied to the recipient's tweet.</summary>
    Reply = 2,

    /// <summary>Someone retweeted the recipient's tweet.</summary>
    Retweet = 3,
}
