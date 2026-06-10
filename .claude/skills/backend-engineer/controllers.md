# Controller / Endpoint Patterns

Reference for minimal API endpoints, DTOs, validation, and error handling.

---

## Minimal APIs

Endpoints are static classes in `Controllers/`, one per domain. The class extends
`RouteGroupBuilder` — the group is created in `Program.cs` via `app.MapGroup(...)`.

```csharp
// Controllers/SourceEndpoints.cs
public static class LogSourceEndpoints
{
    public static RouteGroupBuilder MapLogSourceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/",          GetAll);
        group.MapPost("/",         Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/test", Test);

        return group;
    }

    private static async Task<IResult> GetAll(SourceService svc, CancellationToken ct)
    {
        var sources = await svc.GetAllAsync(ct);
        return Results.Ok(sources.Select(ToResponse));
    }

    private static async Task<IResult> Create(
        CreateSourceRequest request, SourceService svc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                { "name", ["Name is required"] }
            });
        }

        var source = await svc.CreateAsync(request, ct);
        return Results.Created($"/api/v1/sources/{source.Id}", ToResponse(source));
    }

    // ...
}

// Program.cs
app.MapGroup("/api/v1/sources").MapLogSourceEndpoints();
app.MapGroup("/api/v1/tags").MapTagEndpoints();
app.MapGroup("/api/v1/saved-searches").MapSavedSearchEndpoints();
app.MapGroup("/api/v1/alerts").MapAlertEndpoints();
app.MapGroup("/api/v1/logs").MapLogEndpoints();
```

---

## Route Conventions

- All routes under `/api/v1/`
- Plural resource names: `/sources`, `/tags`, `/saved-searches`, `/alerts`
- GUID route parameters: `{id:guid}`
- Action routes as verbs: `/sources/{id}/test`, `/logs/query`

---

## DTOs (Request / Response Records)

Use records. **Co-location rule**: response DTOs are defined at the bottom of the endpoint file;
request DTOs are defined at the bottom of the service file. No separate `Models/` directory.

```csharp
// At the bottom of Controllers/SourceEndpoints.cs
public record SourceResponse(
    Guid Id,
    string Name,
    string Type,
    DateTime CreatedAt);

// At the bottom of Application/Services/SourceService.cs
public record CreateSourceRequest(string Name, string Type, JsonElement Config);
public record UpdateSourceRequest(string Name, JsonElement Config);
```

Rules:
- Request DTOs never contain the entity ID — it comes from the route.
- Response DTOs never expose encrypted/sensitive fields (e.g. `EncryptedConfig`).
- Use `JsonElement` for pass-through JSON (e.g. source config before encryption).

---

## Mapping (Entity ↔ DTO)

Private static `ToResponse` method directly in the endpoint class. No AutoMapper.

```csharp
private static SourceResponse ToResponse(Source s) => new(
    Id:        s.Id,
    Name:      s.Name,
    Type:      s.Type.ToString(),
    CreatedAt: s.CreatedAt);
```

---

## Validation

Input validation happens in the endpoint before calling the service. Return
`Results.ValidationProblem(errors)` with a `Dictionary<string, string[]>`.

```csharp
private static async Task<IResult> Create(CreateAlertRequest request, AlertService svc, CancellationToken ct)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Name))
        errors["name"] = ["Name is required"];

    if (request.CheckIntervalSeconds < 60)
        errors["checkIntervalSeconds"] = ["Check interval must be at least 60 seconds"];

    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    var alert = await svc.CreateAsync(request, ct);
    return Results.Created($"/api/v1/alerts/{alert.Id}", ToResponse(alert));
}
```

---

## Error Handling

There is no global exception handler. Endpoints catch typed exceptions from the service and
map them to `Results.*` responses directly.

```csharp
// Application/Exceptions/Exceptions.cs
public class SourceNotFoundException(Guid id) : Exception($"Source {id} was not found");
public class TagNotFoundException(Guid id) : Exception($"Tag {id} was not found");
public class LogSourceQueryException(int statusCode, string body, Exception? inner = null)
    : Exception($"Log source query failed ({statusCode}): {body}", inner);
public class UnsupportedAlertSourceException(string sourceType)
    : Exception($"Alerts are not supported for source type '{sourceType}'");
```

```csharp
// In the endpoint — catch and map explicitly
try
{
    var alert = await svc.CreateAsync(request, ct);
    return Results.Created($"/api/v1/alerts/{alert.Id}", ToResponse(alert));
}
catch (SourceNotFoundException ex)
{
    return Results.NotFound(new { error = ex.Message });
}
catch (UnsupportedAlertSourceException ex)
{
    return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
}
```

Rules:
- Services throw typed exceptions — never `throw new Exception(...)`.
- Exception types live in `Application/Exceptions/Exceptions.cs`.
- The endpoint catches and converts — no uncaught exceptions bubble through.

---

## Service Layer

One service class per domain. Injected via constructor. Services own the business logic;
endpoints are thin routing + mapping.

```csharp
public class AlertService(DashyDbContext db, ILogger<AlertService> logger)
{
    public async Task<List<Alert>> GetAllAsync(CancellationToken ct) { ... }
    public async Task<Alert> CreateAsync(CreateAlertRequest request, CancellationToken ct) { ... }
    public async Task<Alert?> UpdateAsync(Guid id, UpdateAlertRequest request, CancellationToken ct) { ... }
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct) { ... }
}
```

Registration:

```csharp
builder.Services.AddScoped<SourceService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<LogQueryService>();
```

---

## SSE (Server-Sent Events)

`AlertSseService` is a singleton holding active connections behind a per-client write lock.
The endpoint registers itself as a client, runs a heartbeat, and cleans up on disconnect.

```csharp
// Controllers/AlertEndpoints.cs
group.MapGet("/stream", Stream);

private static async Task Stream(HttpContext context, AlertSseService sse)
{
    var response = context.Response;
    response.Headers.ContentType = "text/event-stream";
    response.Headers.CacheControl = "no-cache";
    response.Headers["X-Accel-Buffering"] = "no";

    var ct = context.RequestAborted;
    await response.WriteAsync("retry: 3000\n\n", ct);
    await response.Body.FlushAsync(ct);

    var clientId = sse.AddClient(response);
    try
    {
        using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await heartbeat.WaitForNextTickAsync(ct))
        {
            if (!await sse.WritePingAsync(clientId, ct))
            {
                break;
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Client disconnected.
    }
    finally
    {
        sse.RemoveClient(clientId);
    }
}
```

The background polling service broadcasts to all clients via `IAlertBroadcaster` (which
`AlertSseService` implements). See `background-services.md`.

---

## Response Shape Conventions

| Operation | Status | Body |
|---|---|---|
| List | 200 | `T[]` |
| Get by ID | 200 | `T` |
| Create | 201 | `T` (with `Location` header) |
| Update | 200 | `T` (or 404 if missing) |
| Delete | 204 | — (or 404 if missing) |
| Action (test, query) | 200 | Result object |
| Validation error | 400 | `ValidationProblem` |
| Not found | 404 | `{ error: "..." }` |
| Unsupported operation | 422 | `ProblemDetails` |
