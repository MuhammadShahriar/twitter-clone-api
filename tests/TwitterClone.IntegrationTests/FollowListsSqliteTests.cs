using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Repository-level tests for the follower/following lists (<see cref="UserRepository.GetFollowersAsync"/> and
/// <see cref="UserRepository.GetFollowingAsync"/>) against <b>SQLite</b> (relational). They exercise the
/// keyset over the <c>Follow</c> edge's <c>(CreatedAtUtc, Id)</c>, the lite <c>UserDto</c> projection
/// (correlated follower/following counts + the caller's <c>isFollowedByCurrentUser</c> flag), and pagination
/// with no duplicates/skips — none of which the in-memory provider translates to SQL. Per CLAUDE.md's policy.
/// </summary>
public class FollowListsSqliteTests
{
    private static readonly DateTime Base = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int minutes) => Base.AddMinutes(minutes);

    [Fact]
    public async Task Followers_translate_newest_first_with_followback_flag_and_paginate()
    {
        using var db = new SqliteTestHarness();

        var target = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var viewer = Guid.NewGuid();
        var stranger = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(target, "@fl_target"),
                SqliteTestHarness.NewUser(a, "@fl_a"),
                SqliteTestHarness.NewUser(b, "@fl_b"),
                SqliteTestHarness.NewUser(c, "@fl_c"),
                SqliteTestHarness.NewUser(viewer, "@fl_viewer"),
                SqliteTestHarness.NewUser(stranger, "@fl_stranger"));

            // A, B, C follow the target at increasing times (so newest-first = C, B, A). The stranger does not.
            seed.Follows.AddRange(
                new Follow(a, target) { CreatedAtUtc = At(10) },
                new Follow(b, target) { CreatedAtUtc = At(20) },
                new Follow(c, target) { CreatedAtUtc = At(30) },
                // The viewer follows B (so B shows isFollowedByCurrentUser = true for the viewer) — and B has
                // this extra follower, exercising the correlated FollowerCount.
                new Follow(viewer, b) { CreatedAtUtc = At(40) });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new UserRepository(context);

        var page = await repository.GetFollowersAsync(target, viewer, cursor: null, limit: 50);

        // Most-recently-followed first; the stranger (not a follower) is absent.
        Assert.Equal(new[] { c, b, a }, page.Items.Select(u => u.Id).ToArray());
        Assert.DoesNotContain(page.Items, u => u.Id == stranger);

        // Follow-back flag reflects the viewer: they follow B, not A or C.
        var byId = page.Items.ToDictionary(u => u.Id);
        Assert.True(byId[b].IsFollowedByCurrentUser);
        Assert.False(byId[a].IsFollowedByCurrentUser);
        Assert.False(byId[c].IsFollowedByCurrentUser);

        // Counts are correct: B is followed by the target-follower set member B? B follows target (FollowingCount>=1)
        // and is followed by the viewer (FollowerCount == 1).
        Assert.Equal(1, byId[b].FollowerCount);
        Assert.Equal(1, byId[b].FollowingCount);

        // An anonymous reader sees the same people but no follow-back flags.
        var anon = await repository.GetFollowersAsync(target, currentUserId: null, cursor: null, limit: 50);
        Assert.Equal(new[] { c, b, a }, anon.Items.Select(u => u.Id).ToArray());
        Assert.All(anon.Items, u => Assert.False(u.IsFollowedByCurrentUser));

        // Keyset pagination over the edge-time cursor matches the single-shot order, no duplicates/skips.
        var canonical = page.Items.Select(u => u.Id).ToList();
        var paged = new List<Guid>();
        string? cursor = null;
        for (var guard = 0; guard < 100; guard++)
        {
            var pageN = await repository.GetFollowersAsync(target, viewer, cursor, limit: 2);
            paged.AddRange(pageN.Items.Select(u => u.Id));
            if (pageN.NextCursor is null)
            {
                break;
            }

            cursor = pageN.NextCursor;
        }

        Assert.Equal(canonical, paged);
        Assert.Equal(canonical.Count, paged.Distinct().Count());
    }

    [Fact]
    public async Task Following_translate_newest_first_and_are_disjoint_from_followers()
    {
        using var db = new SqliteTestHarness();

        var target = Guid.NewGuid();
        var x = Guid.NewGuid();
        var y = Guid.NewGuid();
        var follower = Guid.NewGuid();

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(target, "@fg_target"),
                SqliteTestHarness.NewUser(x, "@fg_x"),
                SqliteTestHarness.NewUser(y, "@fg_y"),
                SqliteTestHarness.NewUser(follower, "@fg_follower"));

            // The target follows X then Y; a separate user follows the target (must not appear in "following").
            seed.Follows.AddRange(
                new Follow(target, x) { CreatedAtUtc = At(10) },
                new Follow(target, y) { CreatedAtUtc = At(20) },
                new Follow(follower, target) { CreatedAtUtc = At(30) });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new UserRepository(context);

        // Following = who the target follows, newest-first (Y then X); the follower-of-target is absent.
        var following = await repository.GetFollowingAsync(target, currentUserId: null, cursor: null, limit: 50);
        Assert.Equal(new[] { y, x }, following.Items.Select(u => u.Id).ToArray());
        Assert.DoesNotContain(following.Items, u => u.Id == follower);

        // Followers = who follows the target — just the follower; X and Y are absent.
        var followers = await repository.GetFollowersAsync(target, currentUserId: null, cursor: null, limit: 50);
        Assert.Equal(new[] { follower }, followers.Items.Select(u => u.Id).ToArray());
    }

    [Fact]
    public async Task Lists_carry_the_avatar_url()
    {
        using var db = new SqliteTestHarness();

        var target = Guid.NewGuid();
        var withAvatar = Guid.NewGuid();
        const string avatarUrl = "https://images.test/fl-avatar.png";

        await using (var seed = db.NewContext())
        {
            seed.Users.AddRange(
                SqliteTestHarness.NewUser(target, "@fl_av_target"),
                SqliteTestHarness.NewUser(withAvatar, "@fl_av_follower", avatarUrl));

            seed.Follows.Add(new Follow(withAvatar, target) { CreatedAtUtc = At(10) });

            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var repository = new UserRepository(context);

        var page = await repository.GetFollowersAsync(target, currentUserId: null, cursor: null, limit: 50);
        Assert.Equal(avatarUrl, page.Items.Single().AvatarUrl);
    }
}
