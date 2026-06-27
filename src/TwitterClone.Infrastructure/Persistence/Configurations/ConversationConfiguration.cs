using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Infrastructure.Persistence.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    // Two Guid strings (36 chars each) plus the ':' separator.
    private const int PairKeyMaxLength = 73;

    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.PairKey)
            .IsRequired()
            .HasMaxLength(PairKeyMaxLength);

        builder.Property(c => c.LastMessageAtUtc).IsRequired();

        // Exactly one conversation per unordered pair — the DB backstop for idempotent get-or-create.
        builder.HasIndex(c => c.PairKey).IsUnique();

        // The conversation list sorts by recency.
        builder.HasIndex(c => c.LastMessageAtUtc);
    }
}
