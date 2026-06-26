using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Application.Common.Models;
using TwitterClone.Infrastructure.Authentication;
using TwitterClone.Infrastructure.Identity;
using TwitterClone.Infrastructure.Media;
using TwitterClone.Infrastructure.Notifications;
using TwitterClone.Infrastructure.Persistence;
using TwitterClone.Infrastructure.Persistence.Repositories;

namespace TwitterClone.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ConnectionStringResolver.Resolve(configuration);

        // AddDbContext registers the context as Scoped, so the repositories and the
        // unit of work below all share the SAME context instance per request — which
        // is what lets the UoW commit the changes the repositories staged.
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ITweetRepository, TweetRepository>();
        services.AddScoped<ILikeRepository, LikeRepository>();
        services.AddScoped<IRetweetRepository, RetweetRepository>();
        services.AddScoped<IBookmarkRepository, BookmarkRepository>();
        services.AddScoped<IFollowRepository, FollowRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Notifications (Module 5A): the creation policy (self-skip + unread dedup) lives behind
        // INotificationService; the four social-action handlers stage a notification through it, committed
        // by their own SaveChanges. Scoped so it shares the request's DbContext with the action.
        services.AddScoped<INotificationService, NotificationService>();

        // ASP.NET Core Identity foundation (Module 1A): registers UserManager and the EF-backed user
        // store that later sub-steps build register/login/JWT on. No endpoints, sign-in cookies or
        // JWT are wired here — AddIdentityCore is the API-friendly (cookieless) set. Token providers
        // (password reset / email confirmation) are added in 1B when those flows need them.
        //
        // Lockout policy (brute-force defense): 5 failed attempts locks the account for 15 minutes;
        // AllowedForNewUsers ensures new accounts are lockout-enabled at creation. IdentityService drives
        // this via UserManager's lockout primitives (no SignInManager / web framework needed here).
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        // Auth use-cases (Module 1B): the Application layer's IIdentityService / IJwtTokenGenerator
        // abstractions, implemented here with Identity + JWT. JwtSettings binds the "Jwt" config
        // section (SecretKey supplied out-of-band via user-secret locally / Jwt__SecretKey on Render).
        //
        // Validate the JWT settings at startup (fail-fast): a missing/short signing key or a missing
        // issuer/audience aborts the boot with a clear message rather than surfacing as a runtime 500
        // on the first login. HMAC-SHA256 needs a key of at least 256 bits (32 bytes).
        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .Validate(
                s => !string.IsNullOrWhiteSpace(s.SecretKey)
                     && System.Text.Encoding.UTF8.GetByteCount(s.SecretKey) >= 32,
                "Jwt:SecretKey must be set and at least 32 bytes (256-bit).")
            .Validate(
                s => !string.IsNullOrWhiteSpace(s.Issuer) && !string.IsNullOrWhiteSpace(s.Audience),
                "Jwt:Issuer and Jwt:Audience must be set.")
            .ValidateOnStart();

        services.Configure<RefreshTokenSettings>(configuration.GetSection(RefreshTokenSettings.SectionName));

        // Tweet-edit window (Module 11A): bound from the TweetEdit section into a plain POCO so the
        // Application handler depends on the settings type directly (no IOptions/config package in Application).
        // Missing section ⇒ the default 30-minute window.
        var tweetEditSettings = new TweetEditSettings();
        configuration.GetSection(TweetEditSettings.SectionName).Bind(tweetEditSettings);
        services.AddSingleton(tweetEditSettings);

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IRefreshTokenService, RefreshTokenService>();

        // Cloudinary image storage (Module 2D): the IImageStorageService abstraction lives in Application;
        // the Cloudinary client lives here. CloudName/ApiKey/ApiSecret are validated at startup (fail-fast)
        // so a misconfig aborts the boot rather than 500-ing on the first upload. ApiSecret is a secret
        // (user-secret locally / Cloudinary__ApiSecret on Render). The client is thread-safe -> Singleton.
        services.AddOptions<CloudinarySettings>()
            .Bind(configuration.GetSection(CloudinarySettings.SectionName))
            .Validate(
                s => !string.IsNullOrWhiteSpace(s.CloudName)
                     && !string.IsNullOrWhiteSpace(s.ApiKey)
                     && !string.IsNullOrWhiteSpace(s.ApiSecret),
                "Cloudinary:CloudName, Cloudinary:ApiKey and Cloudinary:ApiSecret must all be set.")
            .ValidateOnStart();
        services.AddSingleton<IImageStorageService, CloudinaryImageStorageService>();

        return services;
    }
}
