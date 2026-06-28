using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TwitterClone.Api.Common;
using TwitterClone.Api.Hubs;
using TwitterClone.Application;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Infrastructure;
using TwitterClone.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Render (and most PaaS) provide the port to bind via the PORT env var.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Clean Architecture composition: each layer registers its own services.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// JWT bearer authentication (Module 1C-i): validates tokens issued by JwtTokenGenerator.
// The HTTP-aware ICurrentUserService implementation lives in the API layer.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddJwtBearerAuthentication();

// Real-time notifications (Module 5B): SignalR hub + the publisher that the commit chokepoint (UnitOfWork)
// uses to push each new notification. Connections are keyed to the user's JWT sub claim so the publisher can
// target Clients.User(recipientId). The publisher implementation lives here (SignalR is an API concern); the
// Application/Infrastructure layers only know the INotificationPublisher abstraction.
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
builder.Services.AddScoped<INotificationPublisher, NotificationHubPublisher>();

// Real-time direct messages (Module 12B): a second hub + publisher on the same chokepoint/socket-auth as 5B.
builder.Services.AddScoped<IMessagePublisher, ChatHubPublisher>();

// Refresh-token cookie settings (Module 1C-ii) — bound here because cookie handling is an API concern.
builder.Services.Configure<AuthCookieSettings>(builder.Configuration.GetSection(AuthCookieSettings.SectionName));

// CORS: origins are config-driven (Cors:AllowedOrigins) so dev and prod stay
// separate. Dev origins live in appsettings.Development.json; prod origins are
// supplied by Render env vars (e.g. Cors__AllowedOrigins__0). Nothing is hardcoded.
const string CorsPolicy = "ConfiguredOrigins";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            // AllowCredentials is required for the browser to send/receive the refresh-token cookie
            // cross-site. It is incompatible with AllowAnyOrigin, hence the explicit origin list above.
            .AllowCredentials());
});

// Basic rate limiting (built into ASP.NET Core): a strict per-IP window on the auth endpoints (complements
// the Identity lockout against brute force) and a looser global window on writes keyed by the authenticated
// user id. Reads, preflight, health and the SignalR hubs are never throttled. Limits are config-driven and
// default high enough that normal use never trips them; the test host sets RateLimiting:Enabled=false.
// Settings are resolved lazily per request (via IOptions) so the effective config — including values the
// integration test host injects after this point — is honoured (an eager read here would miss them).
builder.Services.Configure<RateLimitSettings>(builder.Configuration.GetSection(RateLimitSettings.SectionName));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Surface a Retry-After header when the limiter can tell the client how long to back off.
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };

    // Auth endpoints: strict fixed window per client IP (applied via [EnableRateLimiting("auth")]). When the
    // limiter is disabled (test host) it's a no-op — the middleware must still run so the per-endpoint
    // [EnableRateLimiting] metadata has a handler.
    options.AddPolicy(RateLimitPolicies.Auth, httpContext =>
    {
        var settings = RateLimits(httpContext);
        return settings.Enabled
            ? RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.AuthPermitLimit,
                    Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                })
            : RateLimitPartition.GetNoLimiter("disabled");
    });

    // Global write limiter: leave reads/preflight/health/hubs unthrottled; cap writes per authenticated user
    // id (fallback to IP when anonymous, e.g. login/register, which also carry the stricter auth policy).
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var settings = RateLimits(httpContext);
        var method = httpContext.Request.Method;
        if (!settings.Enabled
            || HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)
            || httpContext.Request.Path.StartsWithSegments("/hubs"))
        {
            return RateLimitPartition.GetNoLimiter("unthrottled");
        }

        var key = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? ClientIp(httpContext);
        return RateLimitPartition.GetFixedWindowLimiter($"write:{key}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.WritePermitLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
        });
    });
});

static RateLimitSettings RateLimits(HttpContext httpContext) =>
    httpContext.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;

// The client IP for partitioning: prefer X-Forwarded-For (Render terminates TLS at a proxy) then the socket.
static string ClientIp(HttpContext httpContext)
{
    var forwarded = httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var values)
        ? values.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
        : null;
    return forwarded ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

// Exception handlers are tried in registration order; each ignores exceptions that aren't its type.
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<AccountLockedExceptionHandler>();
builder.Services.AddExceptionHandler<NotFoundExceptionHandler>();
builder.Services.AddExceptionHandler<ForbiddenAccessExceptionHandler>();
builder.Services.AddExceptionHandler<EditWindowExpiredExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// Apply any pending EF Core migrations on startup so the skeleton deploys cleanly.
// Guarded by IsRelational so non-relational providers (e.g. the in-memory provider
// used by integration tests) skip migration instead of throwing.
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (context.Database.IsRelational())
    {
        context.Database.Migrate();
    }
}

app.UseExceptionHandler();

// Swagger is enabled in all environments so the deployed skeleton is explorable.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors(CorsPolicy);

// CORS runs first (so preflight/headers are handled), then authentication populates the
// principal, then authorization enforces [Authorize] — all before the endpoints run.
app.UseAuthentication();
app.UseAuthorization();

// Rate limiting runs after authentication so the write limiter can partition by the authenticated user id,
// and after routing so the per-endpoint auth policy is in scope. Always wired (the [EnableRateLimiting]
// endpoints require a handler); the limiters themselves are no-ops when RateLimiting:Enabled is false.
app.UseRateLimiter();

app.MapControllers();

// Real-time notification hub. After UseAuthentication/UseAuthorization so [Authorize] applies; the CORS
// policy (AllowCredentials + explicit origins) already set above lets the browser open the cross-site socket.
app.MapHub<NotificationHub>(NotificationHub.Path);
app.MapHub<ChatHub>(ChatHub.Path);

// Lightweight liveness endpoint for Render health checks.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Exposed so the integration test project can drive the real app via WebApplicationFactory<Program>.
public partial class Program;
