using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Identity;

namespace TwitterClone.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    // SHA-256 in hex is 64 chars; 128 leaves headroom.
    private const int HashLength = 128;

    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(HashLength);

        // Lookups are by hash, and a hash collision/duplicate would be a bug — enforce uniqueness.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.Property(t => t.ReplacedByTokenHash)
            .HasMaxLength(HashLength);

        builder.Property(t => t.ExpiresAtUtc).IsRequired();

        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => t.UserId);

        // FK to the Identity user, configured here so the Domain entity needs no navigation to
        // ApplicationUser. Deleting a user cascades to their refresh tokens.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
