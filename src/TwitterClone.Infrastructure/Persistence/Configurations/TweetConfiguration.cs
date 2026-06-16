using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Infrastructure.Persistence.Configurations;

public class TweetConfiguration : IEntityTypeConfiguration<Tweet>
{
    public void Configure(EntityTypeBuilder<Tweet> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Content)
            .IsRequired()
            .HasMaxLength(Tweet.MaxContentLength);

        builder.Property(t => t.AuthorHandle)
            .IsRequired()
            .HasMaxLength(Tweet.MaxAuthorHandleLength);

        builder.Property(t => t.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(t => t.CreatedAtUtc);
    }
}
