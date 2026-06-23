using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Infrastructure.Persistence.Configurations;

public class TweetMediaConfiguration : IEntityTypeConfiguration<TweetMedia>
{
    public void Configure(EntityTypeBuilder<TweetMedia> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Url)
            .IsRequired()
            .HasMaxLength(TweetMedia.MaxUrlLength);

        builder.Property(m => m.PublicId)
            .IsRequired()
            .HasMaxLength(TweetMedia.MaxPublicIdLength);

        builder.Property(m => m.Position)
            .IsRequired();

        // Media is read back per-tweet, ordered by position; index the FK for that lookup.
        builder.HasIndex(m => m.TweetId);

        // The Tweet -> Media relationship (FK + cascade) is configured from the Tweet side in
        // TweetConfiguration, since the navigation lives on the aggregate root.
    }
}
