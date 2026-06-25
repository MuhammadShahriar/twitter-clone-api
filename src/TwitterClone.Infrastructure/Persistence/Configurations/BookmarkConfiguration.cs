using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Identity;

namespace TwitterClone.Infrastructure.Persistence.Configurations;

public class BookmarkConfiguration : IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.UserId).IsRequired();
        builder.Property(b => b.TweetId).IsRequired();

        // A user can bookmark a tweet at most once — the DB enforces idempotency as a backstop to the
        // handler's existence check (and makes the "bookmarked by me" lookup a single-row hit).
        builder.HasIndex(b => new { b.UserId, b.TweetId }).IsUnique();

        // Index the tweet FK so deleting a tweet (cascade) and the per-tweet lookup stay efficient.
        builder.HasIndex(b => b.TweetId);

        // FK to the Identity user, configured here so the Domain entity needs no navigation to
        // ApplicationUser. Deleting a user cascades to their bookmarks.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to the bookmarked tweet (no navigation either way — the Domain carries only the TweetId value).
        // Deleting a tweet cascades to its bookmarks.
        builder.HasOne<Tweet>()
            .WithMany()
            .HasForeignKey(b => b.TweetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
