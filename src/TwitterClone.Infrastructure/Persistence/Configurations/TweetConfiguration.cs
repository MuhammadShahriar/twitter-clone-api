using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Identity;

namespace TwitterClone.Infrastructure.Persistence.Configurations;

public class TweetConfiguration : IEntityTypeConfiguration<Tweet>
{
    public void Configure(EntityTypeBuilder<Tweet> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Content)
            .IsRequired()
            .HasMaxLength(Tweet.MaxContentLength);

        builder.Property(t => t.AuthorId)
            .IsRequired();

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(t => t.CreatedAtUtc);

        // Feeds will filter/sort by author.
        builder.HasIndex(t => t.AuthorId);

        // Replies are looked up by parent ("give me the replies to this tweet").
        builder.HasIndex(t => t.ParentId);

        // Quotes are looked up by the tweet they quote (the quote-count subquery).
        builder.HasIndex(t => t.QuotedTweetId);

        // FK to the Identity user. The Domain has no navigation to ApplicationUser (it stays
        // Identity-free) — the relationship is enforced here, in Infrastructure, by AuthorId only.
        // Deleting a user removes their tweets.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-reference for replies/threads: a tweet's ParentId points at the tweet it replies to.
        // No navigation property either way (the Domain carries only the ParentId value). Cascade so the
        // database cleans up a deleted tweet's descendants; the delete handler also removes direct replies
        // explicitly so the behaviour holds on non-cascading providers (the in-memory test provider).
        builder.HasOne<Tweet>()
            .WithMany()
            .HasForeignKey(t => t.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Second self-reference for quotes: a quote tweet's QuotedTweetId points at the tweet it embeds.
        // SET NULL (not Cascade) so deleting the quoted tweet nulls the reference in the quotes of it — the
        // quote survives and renders "unavailable" — rather than deleting the quotes or FK-erroring on our
        // hard delete. No navigation property (the Domain carries only the QuotedTweetId value).
        builder.HasOne<Tweet>()
            .WithMany()
            .HasForeignKey(t => t.QuotedTweetId)
            .OnDelete(DeleteBehavior.SetNull);

        // Attached images are a child of the Tweet aggregate (one-to-many, FK TweetMedia.TweetId).
        // Cascade so deleting a tweet removes its images. Map the read-only Media collection via its
        // backing field, since the public surface exposes no setter.
        builder.HasMany(t => t.Media)
            .WithOne()
            .HasForeignKey(m => m.TweetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Tweet.Media))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
