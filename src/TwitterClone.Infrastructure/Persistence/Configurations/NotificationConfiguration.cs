using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Identity;

namespace TwitterClone.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.RecipientId).IsRequired();
        builder.Property(n => n.ActorId).IsRequired();
        builder.Property(n => n.Type).IsRequired();
        builder.Property(n => n.IsRead).IsRequired();

        // Serves the unread-count lookup (filter on RecipientId + IsRead).
        builder.HasIndex(n => new { n.RecipientId, n.IsRead });

        // Serves the newest-first list and its keyset pagination over (CreatedAtUtc, Id).
        builder.HasIndex(n => new { n.RecipientId, n.CreatedAtUtc });

        // FKs to the recipient and the actor (both AspNetUsers): deleting either user removes their
        // notifications. Two cascade paths into AspNetUsers — permitted on PostgreSQL (as with Follows).
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(n => n.ActorId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to the associated tweet (nullable — null for a follow). Deleting the tweet removes the
        // notifications that point at it, so no notification ever references a tweet that no longer exists.
        builder.HasOne<Tweet>()
            .WithMany()
            .HasForeignKey(n => n.TweetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
