using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Identity;

namespace TwitterClone.Infrastructure.Persistence.Configurations;

public class RetweetConfiguration : IEntityTypeConfiguration<Retweet>
{
    public void Configure(EntityTypeBuilder<Retweet> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.TweetId).IsRequired();

        // A user can retweet a tweet at most once — the DB enforces idempotency as a backstop to the
        // handler's existence check (and makes the "retweeted by me" lookup a single-row hit).
        builder.HasIndex(r => new { r.UserId, r.TweetId }).IsUnique();

        // Retweet counts are read per tweet — index the FK for that lookup.
        builder.HasIndex(r => r.TweetId);

        // FK to the Identity user, configured here so the Domain entity needs no navigation to
        // ApplicationUser. Deleting a user cascades to their retweets.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to the retweeted tweet (no navigation either way). Deleting a tweet cascades to its retweets.
        builder.HasOne<Tweet>()
            .WithMany()
            .HasForeignKey(r => r.TweetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
