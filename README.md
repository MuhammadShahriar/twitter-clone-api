# twitter-clone-api

A Twitter clone backend built with **ASP.NET Core (.NET 10)** on **Clean Architecture** with **CQRS via MediatR**.

This repository is **Module 0 — the walking skeleton**: a single end-to-end vertical slice (create + list a tweet) that proves the whole spine works and deploys. It is intentionally minimal but production-shaped.

## Architecture

Clean Architecture with the dependency rule pointing inward (outer layers depend on inner, never the reverse):

```
TwitterClone.Domain          ← entities, no dependencies
        ▲
TwitterClone.Application      ← CQRS commands/queries (MediatR), validation, repository/UoW interfaces
        ▲
TwitterClone.Infrastructure   ← EF Core, PostgreSQL, DbContext + repository/UoW implementations
        ▲
TwitterClone.Api              ← controllers, DI composition, Swagger, host
```

- **Domain** — `Tweet` entity and a small `BaseEntity` (Id + CreatedAtUtc). No framework dependencies.
- **Application** — one feature slice under `Tweets/`: `CreateTweetCommand` (+ validator + handler), `GetTweetsQuery`, and `GetTweetByIdQuery` (+ handlers). MediatR drives CQRS; a `ValidationBehaviour` pipeline runs FluentValidation before each handler. Data access goes through the **Repository + Unit of Work** abstractions (`IRepository<T>`, `ITweetRepository`, `IUnitOfWork`) — EF Core is invisible to this layer.
- **Infrastructure** — `ApplicationDbContext` (EF Core + Npgsql), the repository/unit-of-work implementations (`Repository<T>`, `TweetRepository`, `UnitOfWork`), and `ConnectionStringResolver` (accepts either a standard connection string or a Render-style `DATABASE_URL`). Repositories stage changes; only the unit of work commits.
- **Api** — `TweetsController` (thin, delegates to MediatR), global validation-to-`400` translation, Swagger UI, and EF migrations applied on startup.
- **Tests** — `tests/TwitterClone.IntegrationTests` exercises the create→list HTTP spine through `WebApplicationFactory<Program>` against the EF Core in-memory provider (no live database required).

### Endpoints

| Method | Route               | Description                              |
| ------ | ------------------- | ---------------------------------------- |
| GET    | `/health`           | Liveness check (used by Render).         |
| GET    | `/api/tweets`       | List tweets, newest first.               |
| GET    | `/api/tweets/{id}`  | Get a single tweet (`404` if not found). |
| POST   | `/api/tweets`       | Create a tweet (`201` + `Location`).     |
| GET    | `/swagger`          | Swagger UI (enabled everywhere).         |

`POST` body:

```json
{ "content": "Hello world", "authorHandle": "@you" }
```

## Run locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A PostgreSQL database. The fastest path is Docker:

  ```bash
  docker run --name twitterclone-pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=twitterclone -p 5432:5432 -d postgres:16
  ```

### Configure the connection

Local development reads `ConnectionStrings:DefaultConnection` from `src/TwitterClone.Api/appsettings.json` (defaults to `Host=localhost;Port=5432;Database=twitterclone;Username=postgres;Password=postgres`). Adjust it to match your database, or set a `DATABASE_URL` environment variable to override it.

### Run

```bash
dotnet restore twitter-clone-api.slnx
dotnet build twitter-clone-api.slnx
dotnet test twitter-clone-api.slnx          # optional: runs the integration tests (in-memory DB, no Postgres needed)
dotnet run --project src/TwitterClone.Api
```

The API applies any pending EF Core migrations on startup, so the schema is created automatically. Then open:

- Swagger UI: `http://localhost:5220/swagger`
- Health: `http://localhost:5220/health`

`src/TwitterClone.Api/TwitterClone.Api.http` contains ready-to-run sample requests (usable from Visual Studio or the REST Client extension).

### Working with migrations

```bash
# add a migration (tooling: dotnet tool install --global dotnet-ef)
dotnet ef migrations add <Name> \
  --project src/TwitterClone.Infrastructure \
  --startup-project src/TwitterClone.Api \
  --output-dir Persistence/Migrations

# apply manually (the app also does this on startup)
dotnet ef database update \
  --project src/TwitterClone.Infrastructure \
  --startup-project src/TwitterClone.Api
```

## Deploy to Render

Live deployment: **https://twitter-clone-api-9zoz.onrender.com** (Swagger at `/swagger`).

The app ships a root **Dockerfile** and a **render.yaml** describing a single web
service. The database is an **external free [Neon](https://neon.tech) PostgreSQL**,
not a Render-managed one.

> **Why external Postgres?** Render's managed PostgreSQL — and any Blueprint that
> provisions one — requires a payment method on file, even on the free tier. Pointing
> `DATABASE_URL` at a free Neon database keeps the whole deploy card-free. The app
> doesn't care where Postgres lives; it just reads `DATABASE_URL`.

### Deploy steps

1. Push this repo to GitHub.
2. Create a free PostgreSQL database (e.g. on **Neon**) and copy its connection URL
   (the `postgresql://…?sslmode=require` form).
3. In Render: **New + → Web Service**, point it at this repo. Render auto-detects the
   Dockerfile (runtime: **Docker**). Choose the **Free** instance type and branch `main`.
4. Set the **Health Check Path** to `/health`.
5. Add the environment variables below (`DATABASE_URL` = your Neon URL).
6. **Create Web Service.** Pushes to `main` then auto-deploy.

`render.yaml` mirrors this setup: it declares only the web service and leaves
`DATABASE_URL` as `sync: false` (entered in the dashboard, never committed). You can
also deploy it via **New + → Blueprint**, but you'll still supply `DATABASE_URL` yourself.

### Environment variables

| Variable                 | Required | Description                                                                                                  |
| ------------------------ | -------- | ------------------------------------------------------------------------------------------------------------ |
| `DATABASE_URL`           | Yes (on Render) | PostgreSQL connection URL, e.g. `postgresql://user:pass@host/twitterclone?sslmode=require`. Set it to your external (Neon) database. Takes precedence over `ConnectionStrings__DefaultConnection`. |
| `ASPNETCORE_ENVIRONMENT` | No       | `Production` on Render; `Development` locally. Swagger is enabled in all environments.                        |
| `PORT`                   | No       | Injected by Render; the app binds to it automatically. Defaults to `8080` in the container.                  |

> Notes
> - On startup the app runs `Database.Migrate()`, so the database schema is created on first boot — no manual migration step needed.
> - `DATABASE_URL` may be either a `postgres://…` / `postgresql://…` URI or a standard Npgsql connection string; both are handled. URI values are forced to SSL (which Neon requires).
> - The Free instance sleeps after ~15 min idle and cold-starts in ~15–30s on the next request — expected, not a bug.

## Project layout

```
twitter-clone-api.slnx
Dockerfile
render.yaml
src/
  TwitterClone.Domain/
    Common/BaseEntity.cs
    Entities/Tweet.cs
  TwitterClone.Application/
    Common/Interfaces/IRepository.cs
    Common/Interfaces/ITweetRepository.cs
    Common/Interfaces/IUnitOfWork.cs
    Common/Behaviours/ValidationBehaviour.cs
    Tweets/
      TweetDto.cs
      Commands/CreateTweet/...
      Queries/GetTweets/...
      Queries/GetTweetById/...
    DependencyInjection.cs
  TwitterClone.Infrastructure/
    Persistence/ApplicationDbContext.cs
    Persistence/UnitOfWork.cs
    Persistence/ConnectionStringResolver.cs
    Persistence/Repositories/Repository.cs
    Persistence/Repositories/TweetRepository.cs
    Persistence/Configurations/TweetConfiguration.cs
    Persistence/Migrations/...
    DependencyInjection.cs
  TwitterClone.Api/
    Controllers/TweetsController.cs
    Common/ValidationExceptionHandler.cs
    Program.cs
tests/
  TwitterClone.IntegrationTests/
    TweetsApiTests.cs
```
