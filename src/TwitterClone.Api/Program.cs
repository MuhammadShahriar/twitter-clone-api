using Microsoft.EntityFrameworkCore;
using TwitterClone.Api.Common;
using TwitterClone.Application;
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

builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
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

app.MapControllers();

// Lightweight liveness endpoint for Render health checks.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Exposed so the integration test project can drive the real app via WebApplicationFactory<Program>.
public partial class Program;
