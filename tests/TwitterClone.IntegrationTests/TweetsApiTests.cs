using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TwitterClone.Infrastructure.Persistence;
using Xunit;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Drives the real API end-to-end through <see cref="WebApplicationFactory{TEntryPoint}"/>.
///
/// The DbContext is re-pointed at the EF Core in-memory provider so the test runs in CI
/// with no live Postgres or Docker. Limitation: the in-memory provider is NOT relational —
/// it ignores SQL-level concerns (column types, max-length constraints, real transactions,
/// migrations). It is enough to prove the create→list HTTP spine; correctness that depends
/// on actual Postgres behaviour must be covered by tests against a real database.
/// </summary>
public class TweetsApiTests : IClassFixture<TweetsApiTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public TweetsApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Post_then_get_returns_the_created_tweet()
    {
        var client = _factory.CreateClient();

        // POST /api/tweets -> 201 Created with a valid Location header.
        var createResponse = await client.PostAsJsonAsync(
            "/api/tweets",
            new { content = "Hello, walking skeleton!", authorHandle = "@ada" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createResponse.Headers.Location);

        var created = await createResponse.Content.ReadFromJsonAsync<TweetResponse>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);
        Assert.Equal("Hello, walking skeleton!", created.Content);
        Assert.Equal("@ada", created.AuthorHandle);

        // The Location header should address the new resource and itself return 200.
        var fromLocation = await client.GetAsync(createResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fromLocation.StatusCode);

        // GET /api/tweets -> list contains the tweet we just created.
        var list = await client.GetFromJsonAsync<List<TweetResponse>>("/api/tweets");
        Assert.NotNull(list);
        Assert.Contains(list!, t => t.Id == created.Id && t.Content == "Hello, walking skeleton!");
    }

    private record TweetResponse(Guid Id, string Content, string AuthorHandle, DateTime CreatedAtUtc);

    /// <summary>
    /// Boots the real <c>Program</c> but replaces the Npgsql DbContext registration with the
    /// in-memory provider. Each factory instance gets its own database name for isolation.
    /// </summary>
    public class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"tweets-tests-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Drop the production DbContext registration. AddDbContext also registers an
                // IDbContextOptionsConfiguration<T> that re-applies UseNpgsql, so that must go
                // too — otherwise EF sees both the Npgsql and in-memory providers and throws.
                // Match the config service by generic-type-definition name to avoid taking a
                // dependency on its (internal-ish) namespace.
                var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(ApplicationDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition().Name == "IDbContextOptionsConfiguration`1"))
                    .ToList();

                foreach (var descriptor in toRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }
    }
}
