using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Identity;

namespace TwitterClone.Infrastructure.Persistence.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content)
            .IsRequired()
            .HasMaxLength(Message.MaxContentLength);

        builder.Property(m => m.ConversationId).IsRequired();
        builder.Property(m => m.SenderId).IsRequired();

        // The thread read pages messages within a conversation by time (keyset on (CreatedAtUtc, Id)).
        builder.HasIndex(m => new { m.ConversationId, m.CreatedAtUtc });

        // FK to the conversation — deleting a conversation removes its messages.
        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to the Identity sender — deleting a user removes their messages. (No navigation either way.)
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
