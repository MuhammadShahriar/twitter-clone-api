using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Infrastructure.Persistence;

namespace TwitterClone.IntegrationTests;

/// <summary>
/// Boots the real <c>Program</c> for integration tests, with two test-only adjustments:
///   1. the Npgsql DbContext is replaced with the EF Core in-memory provider (no live DB / Docker), and
///   2. JWT signing settings are supplied in-memory so token generation works without a real secret.
/// Each factory instance gets its own database name, so test classes are isolated from one another.
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"integration-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // SecretKey is never committed; tests provide their own (HMAC-SHA256 needs >= 32 bytes).
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "integration-tests-signing-key-at-least-32-bytes-long!!",
                ["Jwt:Issuer"] = "twitter-clone-tests",
                ["Jwt:Audience"] = "twitter-clone-tests",
                ["Jwt:ExpiryMinutes"] = "60",
                ["RefreshToken:ExpiryDays"] = "7",
                // Secure=false so the cookie is usable over the test server's plain http.
                ["AuthCookie:Name"] = "refresh_token",
                ["AuthCookie:Path"] = "/api/auth",
                ["AuthCookie:SameSite"] = "Lax",
                ["AuthCookie:Secure"] = "false",
                ["AuthCookie:Domain"] = "",
                // Cloudinary settings are validated on startup (fail-fast). The real upload is replaced by a
                // fake below, so these are dummy values that only need to satisfy that validation.
                ["Cloudinary:CloudName"] = "test-cloud",
                ["Cloudinary:ApiKey"] = "test-key",
                ["Cloudinary:ApiSecret"] = "test-secret",
                ["Cloudinary:UploadFolder"] = "tests",
            });
        });

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

            // Swap the Cloudinary-backed image storage for an in-process fake so tests don't hit the
            // network. The CloudinarySettings validation above still runs (hence the dummy config).
            var imageStorage = services.FirstOrDefault(d => d.ServiceType == typeof(IImageStorageService));
            if (imageStorage is not null)
            {
                services.Remove(imageStorage);
            }

            services.AddSingleton<IImageStorageService, FakeImageStorageService>();
        });
    }
}
