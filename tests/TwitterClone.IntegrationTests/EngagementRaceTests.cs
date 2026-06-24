using TwitterClone.Application.Common.Exceptions;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Application.Tweets;
using TwitterClone.Application.Tweets.Commands.LikeTweet;
using TwitterClone.Application.Tweets.Commands.UnlikeTweet;
using TwitterClone.Application.Users;
using TwitterClone.Application.Users.Commands.FollowUser;
using TwitterClone.Application.Users.Commands.UnfollowUser;
using TwitterClone.Domain.Entities;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Handler-level unit tests for the concurrency / unique-violation handling (Fix B1). They use hand-written
/// fakes (no mocking library) and feed the handler the exact race exceptions the Unit of Work translates —
/// <see cref="UniqueConstraintViolationException"/> on the add side and <see cref="ConcurrencyConflictException"/>
/// on the remove side — asserting the handler returns the normal success result instead of letting the
/// exception escape (which would be a 500). Deterministic, no DB needed. The sequential idempotency paths
/// (already-liked / not-liked) are covered by the HTTP integration tests.
/// </summary>
public class EngagementRaceTests
{
    private static readonly Guid Caller = Guid.NewGuid();
    private static readonly Guid SomeTweet = Guid.NewGuid();

    [Fact]
    public async Task Like_add_race_swallows_unique_violation_and_returns_success()
    {
        var uow = new FakeUnitOfWork(new UniqueConstraintViolationException("dup", new Exception()));
        var handler = new LikeTweetCommandHandler(
            new FakeTweetRepository { Exists = true, Dto = SampleTweet() },
            new FakeLikeRepository(existing: null), // null => handler takes the add path that races
            uow,
            new FakeCurrentUser(Caller));

        var result = await handler.Handle(new LikeTweetCommand(SomeTweet), CancellationToken.None);

        Assert.Equal(SomeTweet, result.Id);
        Assert.Equal(1, uow.SaveCalls); // it really attempted the insert (and swallowed the violation)
    }

    [Fact]
    public async Task Unlike_remove_race_swallows_concurrency_conflict_and_returns_success()
    {
        var uow = new FakeUnitOfWork(new ConcurrencyConflictException("gone", new Exception()));
        var handler = new UnlikeTweetCommandHandler(
            new FakeTweetRepository { Exists = true, Dto = SampleTweet() },
            new FakeLikeRepository(existing: new Like(Caller, SomeTweet)), // exists => handler takes remove path
            uow,
            new FakeCurrentUser(Caller));

        var result = await handler.Handle(new UnlikeTweetCommand(SomeTweet), CancellationToken.None);

        Assert.Equal(SomeTweet, result.Id);
        Assert.Equal(1, uow.SaveCalls);
    }

    [Fact]
    public async Task Follow_add_race_swallows_unique_violation_and_returns_success()
    {
        var followee = Guid.NewGuid();
        var uow = new FakeUnitOfWork(new UniqueConstraintViolationException("dup", new Exception()));
        var handler = new FollowUserCommandHandler(
            new FakeUserRepository(id: followee, dto: SampleUser(followee)),
            new FakeFollowRepository(existing: null),
            uow,
            new FakeCurrentUser(Caller));

        var result = await handler.Handle(new FollowUserCommand("@someone"), CancellationToken.None);

        Assert.Equal(followee, result.Id);
        Assert.Equal(1, uow.SaveCalls);
    }

    [Fact]
    public async Task Unfollow_remove_race_swallows_concurrency_conflict_and_returns_success()
    {
        var followee = Guid.NewGuid();
        var uow = new FakeUnitOfWork(new ConcurrencyConflictException("gone", new Exception()));
        var handler = new UnfollowUserCommandHandler(
            new FakeUserRepository(id: followee, dto: SampleUser(followee)),
            new FakeFollowRepository(existing: new Follow(Caller, followee)),
            uow,
            new FakeCurrentUser(Caller));

        var result = await handler.Handle(new UnfollowUserCommand("@someone"), CancellationToken.None);

        Assert.Equal(followee, result.Id);
        Assert.Equal(1, uow.SaveCalls);
    }

    [Fact]
    public async Task Like_does_not_swallow_unrelated_exceptions()
    {
        // Only the race exceptions are caught; anything else must propagate (not silently succeed).
        var uow = new FakeUnitOfWork(new InvalidOperationException("boom"));
        var handler = new LikeTweetCommandHandler(
            new FakeTweetRepository { Exists = true, Dto = SampleTweet() },
            new FakeLikeRepository(existing: null),
            uow,
            new FakeCurrentUser(Caller));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new LikeTweetCommand(SomeTweet), CancellationToken.None));
    }

    private static TweetDto SampleTweet() =>
        new(SomeTweet, "content", Guid.NewGuid(), "@author", "Author", DateTime.UtcNow,
            null, 0, 1, 0, true, false, null, []);

    private static UserDto SampleUser(Guid id) =>
        new(id, "@someone", "Someone", null, null, DateTime.UtcNow, 1, 0, true);

    private sealed class FakeUnitOfWork(Exception? toThrow = null) : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCalls++;
            return toThrow is not null ? Task.FromException<int>(toThrow) : Task.FromResult(1);
        }
    }

    private sealed class FakeCurrentUser(Guid? userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Handle => null;
        public bool IsAuthenticated => userId is not null;
    }

    private sealed class FakeTweetRepository : ITweetRepository
    {
        public bool Exists { get; init; } = true;
        public TweetDto? Dto { get; init; }

        public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Exists);

        public Task<TweetDto?> GetByIdWithAuthorAsync(Guid id, Guid? currentUserId, CancellationToken ct = default) =>
            Task.FromResult(Dto);

        public Task<CursorPage<TweetDto>> GetFeedAsync(Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<CursorPage<TweetDto>> GetRepliesAsync(Guid parentId, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<CursorPage<TweetDto>> GetFollowingFeedAsync(Guid currentUserId, string? cursor, int limit, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<CursorPage<TweetDto>> GetUserTweetsAsync(Guid authorId, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<CursorPage<TweetDto>> GetUserLikedTweetsAsync(Guid likerId, Guid? currentUserId, string? cursor, int limit, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<Tweet>> GetDirectRepliesAsync(Guid parentId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Tweet?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Tweet>> ListAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task AddAsync(Tweet entity, CancellationToken ct = default) => throw new NotImplementedException();
        public void Update(Tweet entity) => throw new NotImplementedException();
        public void Remove(Tweet entity) => throw new NotImplementedException();
    }

    private sealed class FakeLikeRepository(Like? existing) : ILikeRepository
    {
        public Task<Like?> FindAsync(Guid userId, Guid tweetId, CancellationToken ct = default) =>
            Task.FromResult(existing);

        public Task AddAsync(Like entity, CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(Like entity) { }
        public void Update(Like entity) => throw new NotImplementedException();
        public Task<Like?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Like>> ListAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeFollowRepository(Follow? existing) : IFollowRepository
    {
        public Task<Follow?> FindAsync(Guid followerId, Guid followeeId, CancellationToken ct = default) =>
            Task.FromResult(existing);

        public Task AddAsync(Follow entity, CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(Follow entity) { }
        public void Update(Follow entity) => throw new NotImplementedException();
        public Task<Follow?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Follow>> ListAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeUserRepository(Guid? id, UserDto? dto) : IUserRepository
    {
        public Task<UserDto?> GetByHandleAsync(string handle, Guid? currentUserId, CancellationToken ct = default) =>
            Task.FromResult(dto);

        public Task<Guid?> GetIdByHandleAsync(string handle, CancellationToken ct = default) =>
            Task.FromResult(id);

        public Task<IReadOnlyList<UserSuggestionDto>> GetSuggestionsAsync(Guid currentUserId, int limit, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
