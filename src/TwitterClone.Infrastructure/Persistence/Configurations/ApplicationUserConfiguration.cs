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

        // The @handle is the user's unique public identifier.
        builder.HasIndex(u => u.Handle).IsUnique();

        builder.Property(u => u.DisplayName)
            .IsRequired()
            .HasMaxLength(ApplicationUser.MaxDisplayNameLength);

        builder.Property(u => u.Bio)
            .HasMaxLength(ApplicationUser.MaxBioLength);

        builder.Property(u => u.CreatedAtUtc)
            .IsRequired();
    }
}
