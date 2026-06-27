using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Repository-level tests for the DM read queries against <b>SQLite</b> (relational): the conversation list
/// projection (other-participant join, last-message preview sub-select, and the per-caller unread-count
/// correlated subquery with its <c>LastReadAtUtc IS NULL</c> handling), the recency keyset, and the
/// newest-first messages query with <c>isMine</c> — none of which the in-memory provider verifies for real
/// SQL translation.
/// </summary>
public class ConversationsSqliteTests
{
    private static readonly DateTime Base = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int minutes) => Base.AddMinutes(minutes);

    [Fact]
    public async Task Conversation_list_projects_other_participant_preview_unread_and_orders_by_recency()
    {
        using var db = new SqliteTestHarness();

        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var carol = Guid.NewGuid();
        var c1 = Guid.NewGuid(); // alice <-> bob, newest
        var c2 = Guid.NewGuid(); // alice <-> carol, older

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(alice, "@alice_dm"),
                SqliteTestHarness.NewUser(bob, "@bob_dm", "https://img/bob.png"),
                SqliteTestHarness.NewUser(carol, "@carol_dm"));

            var conv1 = new Conversation(alice, bob) { Id = c1 };
            conv1.RecordMessageAt(At(30));
            var conv2 = new Conversation(alice, carol) { Id = c2 };
            conv2.RecordMessageAt(At(15));
            seed.Conversations.AddRange(conv1, conv2);

            // alice read conv1 up to t20; never read conv2.
            var pAlice1 = new ConversationParticipant(c1, alice);
            pAlice1.MarkReadAt(At(20));
            var pBob1 = new ConversationParticipant(c1, bob);
            var pAlice2 = new ConversationParticipant(c2, alice);
            var pCarol2 = new ConversationParticipant(c2, carol);
            seed.ConversationParticipants.AddRange(pAlice1, pBob1, pAlice2, pCarol2);

            seed.Messages.AddRange(
                new Message(c1, bob, "bob first") { Id = Guid.NewGuid(), CreatedAtUtc = At(10) },
                new Message(c1, alice, "alice mid") { Id = Guid.NewGuid(), CreatedAtUtc = At(20) },
                new Message(c1, bob, "bob latest") { Id = Guid.NewGuid(), CreatedAtUtc = At(30) },
                new Message(c2, carol, "carol hi") { Id = Guid.NewGuid(), CreatedAtUtc = At(15) });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var conversations = new ConversationRepository(context);

        var page = await conversations.GetConversationsAsync(alice, cursor: null, limit: 50);

        // Ordered most-recent first: conv1 (t30) then conv2 (t15).
        Assert.Equal(new[] { c1, c2 }, page.Items.Select(x => x.Id).ToArray());

        var dm1 = page.Items.Single(x => x.Id == c1);
        Assert.Equal("@bob_dm", dm1.OtherParticipant.Handle);          // the OTHER participant, not alice
        Assert.Equal("https://img/bob.png", dm1.OtherParticipant.AvatarUrl);
        Assert.Equal("bob latest", dm1.LastMessage!.ContentPreview);    // most-recent message
        Assert.Equal(bob, dm1.LastMessage.SenderId);
        Assert.Equal(1, dm1.UnreadCount);                               // only bob's t30 (after alice's read@t20)

        var dm2 = page.Items.Single(x => x.Id == c2);
        Assert.Equal("@carol_dm", dm2.OtherParticipant.Handle);
        Assert.Equal(1, dm2.UnreadCount);                              // never read => carol's message is unread

        // Badge: both conversations have unread for alice.
        Assert.Equal(2, await conversations.GetUnreadConversationCountAsync(alice));

        // From bob's side of conv1: unread excludes bob's OWN messages (only alice's t20 counts).
        var bobList = await conversations.GetConversationsAsync(bob, cursor: null, limit: 50);
        Assert.Equal(1, bobList.Items.Single(x => x.Id == c1).UnreadCount);
        Assert.Equal("@alice_dm", bobList.Items.Single(x => x.Id == c1).OtherParticipant.Handle);
    }

    [Fact]
    public async Task Messages_paginate_newest_first_with_isMine_and_no_dupes()
    {
        using var db = new SqliteTestHarness();

        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var c1 = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(alice, "@alice_m"),
                SqliteTestHarness.NewUser(bob, "@bob_m"));
            seed.Conversations.Add(new Conversation(alice, bob) { Id = c1 });
            seed.ConversationParticipants.AddRange(
                new ConversationParticipant(c1, alice), new ConversationParticipant(c1, bob));

            for (var i = 0; i < 5; i++)
            {
                var sender = i % 2 == 0 ? bob : alice;
                seed.Messages.Add(new Message(c1, sender, $"m{i}") { Id = Guid.NewGuid(), CreatedAtUtc = At(i) });
            }

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var messages = new MessageRepository(context);

        var firstPage = await messages.GetMessagesAsync(c1, alice, cursor: null, limit: 50);
        // Newest-first.
        Assert.Equal(new[] { "m4", "m3", "m2", "m1", "m0" }, firstPage.Items.Select(m => m.Content).ToArray());
        // isMine reflects the caller (alice sent the odd-indexed messages).
        Assert.True(firstPage.Items.Single(m => m.Content == "m3").IsMine);
        Assert.False(firstPage.Items.Single(m => m.Content == "m4").IsMine);
        Assert.Equal("@bob_m", firstPage.Items.Single(m => m.Content == "m4").Sender.Handle);

        // Keyset pagination matches the single-shot order with no dupes/skips.
        var paged = new List<string>();
        string? cursor = null;
        for (var guard = 0; guard < 50; guard++)
        {
            var p = await messages.GetMessagesAsync(c1, alice, cursor, limit: 2);
            paged.AddRange(p.Items.Select(m => m.Content));
            if (p.NextCursor is null) break;
            cursor = p.NextCursor;
        }

        Assert.Equal(new[] { "m4", "m3", "m2", "m1", "m0" }, paged.ToArray());
        Assert.Equal(5, paged.Distinct().Count());
    }
}
