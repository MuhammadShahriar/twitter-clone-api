using Microsoft.EntityFrameworkCore;
using TwitterClone.Api.Common;
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

// Exception handlers are tried in registration order; each ignores exceptions that aren't its type.
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<AccountLockedExceptionHandler>();
builder.Services.AddExceptionHandler<NotFoundExceptionHandler>();
builder.Services.AddExceptionHandler<ForbiddenAccessExceptionHandler>();
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

app.MapControllers();

// Lightweight liveness endpoint for Render health checks.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Exposed so the integration test project can drive the real app via WebApplicationFactory<Program>.
public partial class Program;
