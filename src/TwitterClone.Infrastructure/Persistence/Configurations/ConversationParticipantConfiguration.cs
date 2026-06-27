using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Identity;

namespace TwitterClone.Infrastructure.Persistence.Configurations;

public class ConversationParticipantConfiguration : IEntityTypeConfiguration<ConversationParticipant>
{
    public void Configure(EntityTypeBuilder<ConversationParticipant> builder)
    {
        // Composite key — one membership row per (conversation, user).
        builder.HasKey(p => new { p.ConversationId, p.UserId });

        builder.Property(p => p.LastReadAtUtc);

        // "My conversations" is a lookup by UserId, which the composite PK (ConversationId first) doesn't serve.
        builder.HasIndex(p => p.UserId);

        // FK to the conversation — deleting a conversation removes its participant rows.
        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(p => p.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to the Identity user — deleting a user removes their memberships. (No navigation either way.)
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
