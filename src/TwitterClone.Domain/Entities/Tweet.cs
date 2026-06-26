using TwitterClone.Domain.Common;

namespace TwitterClone.Domain.Entities;

/// <summary>
/// A single tweet. Owned by the user who created it, referenced by <see cref="AuthorId"/>
/// (a plain <see cref="Guid"/>) — the Domain stays free of any auth/Identity dependency, so there is
/// deliberately no navigation to the Identity user. The FK to the user table is configured in
/// Infrastructure.
/// </summary>
public class Tweet : BaseEntity
{
    /// <summary>Maximum length of a tweet's text, matching Twitter's classic limit.</summary>
    public const int MaxContentLength = 280;

    // Backing field for the Media aggregate child. EF Core reads/writes this field directly (the public
    // surface is read-only); media is only ever added through AddMedia, so the collection stays consistent.
    private readonly List<TweetMedia> _media = [];

    // Parameterless constructor for EF Core materialization only.
    private Tweet()
    {
    }

    /// <summary>
    /// Creates a tweet authored by <paramref name="authorId"/>. Pass <paramref name="parentId"/> to make
    /// this a reply to an existing tweet; leave it null for a top-level tweet. Pass
    /// <paramref name="quotedTweetId"/> to make this a quote tweet (a top-level tweet that embeds another) —
    /// independent of <paramref name="parentId"/> (a quote is top-level, not a reply).
    /// </summary>
    public Tweet(string content, Guid authorId, Guid? parentId = null, Guid? quotedTweetId = null)
    {
        Content = content;
        AuthorId = authorId;
        ParentId = parentId;
        QuotedTweetId = quotedTweetId;
        IsQuote = quotedTweetId.HasValue;
    }

    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// The id of the user who wrote this tweet (their <c>AspNetUsers.Id</c>). The author is taken from
    /// the authenticated principal at creation time, never from the request body.
    /// </summary>
    public Guid AuthorId { get; private set; }

    /// <summary>
    /// When set, the id of the tweet this one replies to (a self-reference). <c>null</c> for a top-level
    /// tweet. The self-referencing FK and its index/cascade live in Infrastructure (the Domain stays free
    /// of any persistence concern); there is deliberately no navigation property to the parent tweet.
    /// </summary>
    public Guid? ParentId { get; private set; }

    /// <summary>
    /// When set, the id of the tweet this one <b>quotes</b> (a self-reference, independent of
    /// <see cref="ParentId"/>) — a quote tweet is a top-level tweet that embeds another. <c>null</c> for a
    /// non-quote. The self-FK is configured in Infrastructure with <c>ON DELETE SET NULL</c> (deleting the
    /// quoted tweet nulls this reference so the quote survives and renders "unavailable"); there is no
    /// navigation to the quoted tweet (the Domain carries only the id value, like <see cref="ParentId"/>).
    /// </summary>
    public Guid? QuotedTweetId { get; private set; }

    /// <summary>
    /// Whether this tweet was created as a quote (set once at creation from <c>quotedTweetId</c>, never
    /// changed). This is the <b>delete-surviving</b> "is a quote" signal: the <see cref="QuotedTweetId"/>
    /// self-FK is <c>SET NULL</c>, so deleting the quoted tweet nulls that id — but <see cref="IsQuote"/>
    /// stays <c>true</c>. The read side uses it to tell a non-quote (renders nothing) from a quote whose
    /// target was deleted (<c>IsQuote</c> true but no preview ⇒ renders "This post is unavailable").
    /// </summary>
    public bool IsQuote { get; private set; }

    /// <summary>
    /// The images attached to this tweet, in attachment order. A navigation collection (unlike the
    /// author/parent references) because the media is a child of the Tweet aggregate, not a cross-aggregate
    /// reference — so it is created and persisted together with the tweet. Read-only outside the aggregate;
    /// add via <see cref="AddMedia"/>. The FK/cascade is configured in Infrastructure.
    /// </summary>
    public IReadOnlyList<TweetMedia> Media => _media.AsReadOnly();

    /// <summary>
    /// When this tweet was last edited (UTC), or <c>null</c> if it has never been edited. The read side
    /// surfaces it so the UI can show an "edited" marker. Set by <see cref="Edit"/>; <see cref="CreatedAtUtc"/>
    /// is never changed by an edit.
    /// </summary>
    public DateTime? EditedAtUtc { get; private set; }

    /// <summary>Attaches an uploaded image (its hosted URL + storage public id) to this tweet.</summary>
    public void AddMedia(string url, string publicId) =>
        _media.Add(new TweetMedia(url, publicId, _media.Count));

    /// <summary>
    /// Edits the tweet's text and stamps <see cref="EditedAtUtc"/>. Text only (v1) — author, parent, quoted
    /// reference, media, counts and <see cref="CreatedAtUtc"/> are all left untouched. The caller enforces the
    /// author-only and edit-window rules; this just applies the change.
    /// </summary>
    public void Edit(string content, DateTime editedAtUtc)
    {
        Content = content;
        EditedAtUtc = editedAtUtc;
    }
}
