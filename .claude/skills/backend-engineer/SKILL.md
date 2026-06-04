---
name: backend-engineer
description: Use when implementing any backend feature, endpoint, service, migration, or test in the .NET 10 + EF Core + SQLite backend. Routes to focused reference files for domain-specific patterns.
---

# Backend Engineer

You are a senior backend engineer on this stack. Follow these patterns exactly unless the user
explicitly says otherwise. When something is "not yet defined", default to the closest existing
pattern in this document and note the assumption.

**Load the right reference file for your task** using the Read tool:

| Task involves | Read file |
|---|---|
| EF Core entities, DbContext, queries, relationships | `data-access.md` (in this skill's directory) |
| Database migrations, schema changes, seeding | `data-access.md` |
| REST endpoints, error handling, DTOs, validation | `controllers.md` |
| Background services, alert polling, scheduled work | `background-services.md` |
| Tests, WebApplicationFactory, integration/unit testing | `testing.md` |
| Querying Azure App Insights (KQL, API key auth, response mapping) | `app-insights-api.md` |

Read multiple files if a task spans domains. Only read what you need.

---

## Stack

| Concern | Technology |
|---|---|
| Language | C# 14 |
| Framework | .NET 10, ASP.NET Core Web API (minimal APIs) |
| Data access | EF Core (SQLite provider — `Microsoft.EntityFrameworkCore.Sqlite`) |
| Database | SQLite (file, mounted as Docker volume) |
| Models | Records for DTOs, classes for EF entities |
| Auth | None — single-user local application |
| JSON | System.Text.Json, `camelCase` globally (default) |
| Migrations | EF Core code-first migrations |
| Background jobs | `IHostedService` / `BackgroundService` with `PeriodicTimer` |
| Real-time | Server-Sent Events (SSE) |
| Testing | xUnit, `WebApplicationFactory<Program>`, in-memory SQLite for tests |
| Infra | Docker Compose (`dashy-api`, `dashy-web`) — no separate DB container |

---

## Coding Conventions

Follow the [Microsoft C# coding conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) with these additional rules:

- **Always use `var`** for local variable declarations.
- **Always use braces `{}`** for `if`, `else`, `foreach`, `for`, and `while` bodies — even single-line statements.
- **No single-line control flow** — the body always goes on the next line inside braces.

```csharp
// ✅ Correct
if (source is null)
{
    return null;
}

foreach (var item in items)
{
    results.Add(item);
}

// ❌ Wrong
if (source is null) return null;
foreach (var item in items) results.Add(item);
if (source is null)
    return null;
```

---

## Core Values

- **Readability first** — duplicate code is fine if it makes each case self-contained. Explicit over clever.
- **Quality** — don't cut corners on null safety, validation, or error handling. If something feels fragile, name it as a follow-up.
- **Testing — affected scope only** — only write/update tests directly touched by the current change. Always state: *"Affected tests: [list]"*
- **Opportunistic refactoring** — when you touch a file and notice code smells (duplication, unclear naming, dead code, overly complex logic), fix them in the same change. Leave every file cleaner than you found it.

---

## Project Structure

```
backend/
  Dashy.Api/                  # ASP.NET Core Web API project
    Program.cs                # Host builder, service registration, middleware, endpoint mapping
    Endpoints/                # Minimal API endpoint groups (static classes)
    Services/                 # Business logic
    Data/
      DashyDbContext.cs       # EF Core DbContext
      Entities/               # EF entity classes
      Configurations/         # IEntityTypeConfiguration<T> files
      Migrations/             # EF Core generated migrations
    Models/                   # Request/response DTOs (records)
    BackgroundServices/       # IHostedService implementations
    Infrastructure/           # Cross-cutting: encryption, SSE, external API clients
  Dashy.Api.Tests/            # Test project
  Dashy.sln                   # Solution file
```

---

## Layer Architecture (ADR-004)

Pragmatic layered architecture enforced by folder conventions within the single `Dashy.Api` project:

| Layer | Folders | Depends on |
|---|---|---|
| **Domain** | `Data/Entities`, `Models` | Nothing — no EF Core, ASP.NET, or HTTP deps |
| **Application** | `Services` | Domain + infrastructure **interfaces** (`ILogSourceAdapter`, `IEncryptionService`) |
| **Infrastructure** | `Infrastructure`, `Data` | Application interfaces (implements them) |
| **Api** | `Endpoints`, `Program.cs` | Everything (DI wiring layer) |

**Boundary rules:**
- Endpoints never touch `DashyDbContext` directly — always via Application services.
- Application services depend on infrastructure only through interfaces defined in `Services/`.
- No repository/unit-of-work abstraction over EF Core (ADR-001 locks in SQLite).
- `ILogSourceAdapter` is the extension point — adding a new source = one adapter + DI registration.

---

## Configuration

- All config via the options pattern — `IOptions<T>` / `IOptionsSnapshot<T>`.
- Constructor injection throughout — never service locator (`IServiceProvider.GetService`).
- Sensitive values (`ENCRYPTION_KEY`, `POSTGRES_CONNECTION_STRING`) come from environment variables.

```csharp
public class DatabaseOptions
{
    public const string Section = "Database";
    public string ConnectionString { get; init; } = "";
}

// In Program.cs
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.Section));
```

---

## Security

- Single-user application — no authentication or authorization middleware.
- API keys for log sources are encrypted at rest in the SQLite database using AES-256-GCM.
- Encryption key held in environment variable (`ENCRYPTION_KEY`), never checked into source.
- Source credentials are never returned to the browser — API responses omit or mask them.

### App Insights authentication (API key method)

Dashy authenticates to the App Insights query API using an **API key + Application ID** — not a connection string, not OAuth.

- The user generates an API key in: Azure Portal → App Insights resource → Configure → API Access → Create API key (Read telemetry permission only)
- The **Application ID** (a GUID) is also on that page — it is NOT the Instrumentation Key
- Dashy stores both encrypted in the `sources.config` JSONB column: `{ "appId": "...", "apiKey": "..." }`
- Every query request sends: `X-Api-Key: <apiKey>` header to `https://api.applicationinsights.io/v1/apps/{appId}/query`
- See `app-insights-api.md` for the full query API reference, KQL examples, and response mapping

---

## Migrations

- Every schema change = a new EF Core migration. Never edit an existing migration.
- Generate via: `dotnet ef migrations add <DescriptiveName>`
- `snake_case` column and table names via explicit `.ToTable()` / `.HasColumnName()` in entity config (SQLite has no automatic name translator).
- Always include `CreatedAt` (default `DateTime.UtcNow`) on new entities.
- UUIDs as primary keys: `Guid` type, generated in application code (`Guid.NewGuid()`) — SQLite has no `gen_random_uuid()`.
- Column defaults and constraints defined in `IEntityTypeConfiguration<T>`, not on the entity class.
- SQLite does not support `ALTER COLUMN` — destructive column changes require a table rebuild migration.

---

## Logging

Use `ILogger<T>` via constructor injection. The framework provides request logging — don't duplicate it.

```csharp
public class SourceService(ILogger<SourceService> logger, DashyDbContext db)
{
    public async Task<List<Source>> GetAllAsync(CancellationToken ct)
    {
        logger.LogDebug("Listing all sources");
        return await db.Sources.ToListAsync(ct);
    }

    public async Task<Source> CreateAsync(CreateSourceRequest request, CancellationToken ct)
    {
        logger.LogInformation("Creating source {Name} type={Type}", request.Name, request.Type);
        // ...
    }
}
```

Use `LogDebug` for read operations, `LogInformation` for writes/mutations, `LogWarning` for recoverable issues, `LogError` for failures.

---

## Implementing a change — checklist

1. **Identify the layer(s)**: endpoint / service / entity / migration / background service.
2. **Data access**: new entity or query? → read `data-access.md`
3. **REST/errors**: endpoint conventions, DTOs, validation? → read `controllers.md`
4. **Background work**: scheduled or event-driven? → read `background-services.md`
5. **Migration**: schema change → generate a new EF Core migration.
6. **Tests**: affected tests only → read `testing.md`
7. **Refactor**: scan touched files for code smells and fix them in the same change.
8. **Flag anything fragile** or where a pattern isn't yet defined — note it as a follow-up.
