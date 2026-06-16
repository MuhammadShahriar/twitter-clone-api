# CLAUDE.md

Guidance for Claude Code (and humans) working in this repository.

## What this is

`twitter-clone-api` — an ASP.NET Core (.NET 10) Web API for a Twitter clone, built on **Clean Architecture** with **CQRS via MediatR**. This is **Module 0: the walking skeleton** — one vertical slice (create + list a tweet) proving the spine end-to-end and deploying to Render. Keep it minimal and production-shaped; do not add features beyond what a module specifies.

## Solution layout

Four projects under `src/`, wired with the Clean Architecture dependency rule (dependencies point inward only):

- **TwitterClone.Domain** — entities (`Tweet`, `BaseEntity`). No external dependencies. The innermost layer.
- **TwitterClone.Application** — use cases as MediatR requests, organised by feature under `Tweets/`. Holds the data-access abstractions (`IRepository<T>`, `ITweetRepository`, `IUnitOfWork`), FluentValidation validators, and the `ValidationBehaviour` pipeline. Depends only on Domain — **EF Core is invisible here** (no `DbContext`, no `DbSet`, no `IQueryable`).
- **TwitterClone.Infrastructure** — `ApplicationDbContext` (EF Core + Npgsql, concrete and confined to this layer), the repository/unit-of-work implementations (`Repository<T>`, `TweetRepository`, `UnitOfWork`), entity configurations, migrations, and `ConnectionStringResolver`. Depends on Application.
- **TwitterClone.Api** — controllers, DI composition (`Program.cs`), Swagger, exception handling. Depends on Application + Infrastructure. The only runnable project.
- **tests/TwitterClone.IntegrationTests** — xUnit + `WebApplicationFactory<Program>` integration tests (EF Core in-memory provider, no live DB needed).

Solution file is `twitter-clone-api.slnx` (modern XML format; opens in Visual Studio 2026).

## Conventions

- **CQRS**: every use case is a MediatR `IRequest`. Commands mutate, queries read. One folder per use case: `Tweets/Commands/CreateTweet/` holds the command (record), handler, and validator together.
- **Controllers are thin**: they only build a request and call `ISender.Send(...)`. No business logic in the API layer.
- **Data access is Repository + Unit of Work**: handlers depend on `ITweetRepository` (entity-specific queries) / `IRepository<T>` and `IUnitOfWork` — **never** on `DbContext` or EF Core. Repositories **never** call `SaveChanges`; they only stage changes (`AddAsync`/`Update`/`Remove`). Only `IUnitOfWork.SaveChangesAsync` commits, so multiple repository operations land in one transaction. Repositories share the same scoped `DbContext` instance via DI. Reads use `AsNoTracking()` and push ordering/filtering down to the database; expose named methods (e.g. `GetAllNewestFirstAsync`) rather than leaking `IQueryable`.
- **Validation**: add a `FluentValidation.AbstractValidator<TCommand>` next to the command. The `ValidationBehaviour` runs it automatically; failures surface as RFC 7807 `400` responses via `ValidationExceptionHandler`.
- **DTOs**: return records (e.g. `TweetDto`) from handlers, never domain entities.
- **DI**: each layer exposes an `AddXxx()` extension (`AddApplication`, `AddInfrastructure`) called from `Program.cs`.

## Adding a feature (the pattern)

1. (If needed) add/extend a Domain entity + its `IEntityTypeConfiguration`. For a new aggregate, add an `I<Entity>Repository : IRepository<Entity>` interface in `Application/Common/Interfaces`, implement it in `Infrastructure/Persistence/Repositories` (inherit `Repository<Entity>`), and register it `Scoped` in `Infrastructure/DependencyInjection`.
2. Add a command/query record implementing `IRequest<TResult>` under `Application/<Feature>/...`.
3. Add the handler (inject the repository + `IUnitOfWork`; commands stage changes then call `SaveChangesAsync`) and, for commands, a FluentValidation validator beside it.
4. Add a DTO record for the result.
5. Add or extend a thin controller action that sends the request.
6. If the schema changed, add an EF migration (see below).

## Commands

```bash
# build / run / test
dotnet build twitter-clone-api.slnx
dotnet run --project src/TwitterClone.Api      # Swagger at /swagger
dotnet test twitter-clone-api.slnx             # integration tests (in-memory DB)

# EF Core migrations (needs: dotnet tool install --global dotnet-ef)
dotnet ef migrations add <Name> \
  --project src/TwitterClone.Infrastructure \
  --startup-project src/TwitterClone.Api \
  --output-dir Persistence/Migrations
```

## Database & config

- PostgreSQL via Npgsql. Local connection comes from `ConnectionStrings:DefaultConnection` in `appsettings.json`; `DATABASE_URL` (a `postgres://…` URI or plain connection string) overrides it and is what Render supplies.
- The app runs `Database.Migrate()` on startup (guarded by `Database.IsRelational()`, so the in-memory test provider skips it), so deploys self-provision the schema.
- Both the runtime DI registration and the design-time `ApplicationDbContextFactory` resolve the connection string through the single `ConnectionStringResolver` (URI/SSL handling lives in one place — they can't drift). The factory lets `dotnet ef` work without booting the API host.

## Deployment

Root `Dockerfile` (multi-stage, .NET 10) + `render.yaml` blueprint. The app binds to Render's `PORT`. See `README.md` for the full deploy walkthrough and env vars.

## Guardrails

- Respect the dependency direction — Domain/Application must not reference Infrastructure or the API.
- Keep modules scoped: build only what the current module asks for.
- Prefer the existing patterns above over introducing new abstractions.
