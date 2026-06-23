using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Identity;

namespace TwitterClone.Infrastructure.Persistence.Configurations;

public class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FollowerId).IsRequired();
        builder.Property(f => f.FolloweeId).IsRequired();

        // A user can follow another at most once. The composite index also serves the "does A follow B?"
        // lookup and (FollowerId-prefixed) the "who does A follow?" / following-feed query.
        builder.HasIndex(f => new { f.FollowerId, f.FolloweeId }).IsUnique();

        // Follower count and the "followed by me" check read by followee — index that side too.
        builder.HasIndex(f => f.FolloweeId);

        // Both ends are FKs to the Identity user (no navigation either way — the Domain carries only the
        // id values). Cascade so deleting a user removes the follow edges they are part of. Postgres
        // permits the two cascade paths into AspNetUsers.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.FollowerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.FolloweeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
