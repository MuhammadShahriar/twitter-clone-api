using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwitterClone.Infrastructure.Identity;

namespace TwitterClone.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the profile fields layered onto <see cref="ApplicationUser"/>. The base Identity schema
/// (AspNetUsers and friends) is configured by <c>IdentityDbContext.OnModelCreating</c>; this only
/// adds the Twitter-clone columns and the unique <c>Handle</c> index.
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.Handle)
            .IsRequired()
            .HasMaxLength(ApplicationUser.MaxHandleLength);

        builder.Property(u => u.NormalizedHandle)
            .IsRequired()
            .HasMaxLength(ApplicationUser.MaxHandleLength);

        // The normalized @handle is the user's unique public identifier — uniqueness (and every handle
        // lookup) runs on the canonical form, so handles are unique case-insensitively.
        builder.HasIndex(u => u.NormalizedHandle).IsUnique();

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(ApplicationUser.MaxDisplayNameLength);

        builder.Property(u => u.Bio)
            .HasMaxLength(ApplicationUser.MaxBioLength);

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(ApplicationUser.MaxAvatarUrlLength);

        builder.Property(u => u.CreatedAtUtc)
            .IsRequired();
    }
}
