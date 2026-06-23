using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Identity;

namespace TwitterClone.Infrastructure.Persistence.Configurations;

public class LikeConfiguration : IEntityTypeConfiguration<Like>
{
    public void Configure(EntityTypeBuilder<Like> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.UserId).IsRequired();
        builder.Property(l => l.TweetId).IsRequired();

        // A user can like a tweet at most once — the DB enforces idempotency as a backstop to the
        // handler's existence check (and makes the "like by me" lookup a single-row hit).
        builder.HasIndex(l => new { l.UserId, l.TweetId }).IsUnique();

        // Like counts are read per tweet ("how many likes does this tweet have?") — index the FK for it.
        builder.HasIndex(l => l.TweetId);

        // FK to the Identity user, configured here so the Domain entity needs no navigation to
        // ApplicationUser. Deleting a user cascades to their likes.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to the liked tweet (no navigation either way — the Domain carries only the TweetId value).
        // Deleting a tweet cascades to its likes.
        builder.HasOne<Tweet>()
            .WithMany()
            .HasForeignKey(l => l.TweetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
