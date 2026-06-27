using Microsoft.EntityFrameworkCore;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Conversations;
using TwitterClone.Domain.Entities;

namespace TwitterClone.Infrastructure.Persistence.Repositories;

/// <summary>
/// Message repository. The thread read joins each message to its sender in <c>AspNetUsers</c> (Identity join
/// confined here) and projects into <see cref="MessageDto"/>, newest-first, keyset-paginated over
/// <c>(CreatedAtUtc, Id)</c> — the same cursor shape as the feeds. Sends only stage via the generic
/// repository; the unit of work commits.
/// </summary>
public class MessageRepository(ApplicationDbContext context)
    : Repository<Message>(context), IMessageRepository
{
    public async Task<CursorPage<MessageDto>> GetMessagesAsync(
        Guid conversationId, Guid currentUserId, string? cursor, int limit, CancellationToken ct = default)
    {
        var position = TweetCursor.Decode(cursor);

        var query = Context.Messages.AsNoTracking().Where(m => m.ConversationId == conversationId);
        if (position is not null)
        {
            query = query.Where(m =>
                m.CreatedAtUtc < position.CreatedAtUtc
                || (m.CreatedAtUtc == position.CreatedAtUtc && m.Id.CompareTo(position.Id) < 0));
        }

        var rows = await (
                from m in query
                join u in Context.Users on m.SenderId equals u.Id
                orderby m.CreatedAtUtc descending, m.Id descending
                select new MessageDto(
                    m.Id,
                    m.ConversationId,
                    m.SenderId,
                    new ChatUserDto(u.Id, u.Handle, u.DisplayName, u.AvatarUrl),
                    m.Content,
                    m.CreatedAtUtc,
                    m.SenderId == currentUserId))
            .Take(limit + 1)
            .ToListAsync(ct);

        if (rows.Count <= limit)
        {
            return new CursorPage<MessageDto>(rows, null);
        }

        var page = rows.Take(limit).ToList();
        var last = page[^1];
        return new CursorPage<MessageDto>(page, new TweetCursor(last.CreatedAtUtc, last.Id).Encode());
    }

    public async Task<MessageDto?> GetDtoAsync(Guid messageId, Guid currentUserId, CancellationToken ct = default) =>
        await (
            from m in Context.Messages.AsNoTracking().Where(m => m.Id == messageId)
            join u in Context.Users on m.SenderId equals u.Id
            select new MessageDto(
                m.Id,
                m.ConversationId,
                m.SenderId,
                new ChatUserDto(u.Id, u.Handle, u.DisplayName, u.AvatarUrl),
                m.Content,
                m.CreatedAtUtc,
                m.SenderId == currentUserId))
            .FirstOrDefaultAsync(ct);
}
