# Controller / Endpoint Patterns

Reference for minimal API endpoints, DTOs, validation, and error handling.

---

## Minimal APIs

Endpoints are organised as static classes in `Endpoints/`, one per domain. Each class has a
`MapEndpoints` extension method called from `Program.cs`.

```csharp
// Endpoints/SourceEndpoints.cs
public static class SourceEndpoints
{
    public static void MapSourceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/sources")
            .WithTags("Sources");

        group.MapGet("/", GetAll);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/test", TestConnection);
    }

    private static async Task<Ok<List<SourceResponse>>> GetAll(
        SourceService service, CancellationToken ct)
    {
        var sources = await service.GetAllAsync(ct);
        return TypedResults.Ok(sources.Select(s => s.ToResponse()).ToList());
    }

    private static async Task<Results<Created<SourceResponse>, ValidationProblem>> Create(
        CreateSourceRequest request, SourceService service, CancellationToken ct)
    {
        var source = await service.CreateAsync(request, ct);
        return TypedResults.Created($"/api/v1/sources/{source.Id}", source.ToResponse());
    }

    // ...
}

// Program.cs
app.MapSourceEndpoints();
app.MapTagEndpoints();
app.MapSavedSearchEndpoints();
app.MapAlertEndpoints();
app.MapLogEndpoints();
```

---

## Route Conventions

- All routes under `/api/v1/`
- Plural resource names: `/sources`, `/tags`, `/saved-searches`, `/alerts`
- GUID route parameters: `{id:guid}`
- Action routes as verbs: `/sources/{id}/test`, `/logs/query`
- Group-level tags for OpenAPI: `.WithTags("Sources")`

---

## DTOs (Request / Response Records)

DTOs live in `Models/`. Use records. Request records use `required` for mandatory fields.
Response records mirror the API contract.

```csharp
// Models/Sources/CreateSourceRequest.cs
public record CreateSourceRequest(
    string Name,
    string Type,
    JsonElement Config);

// Models/Sources/SourceResponse.cs
public record SourceResponse(
    Guid Id,
    string Name,
    string Type,
    DateTimeOffset CreatedAt);

// Models/Sources/TestConnectionResponse.cs
public record TestConnectionResponse(
    bool Ok,
    string? Error = null);
```

Rules:
- Request DTOs never contain the entity ID — it comes from the route.
- Response DTOs never expose encrypted/sensitive fields (e.g. `EncryptedConfig`).
- Use `JsonElement` for pass-through JSON (e.g. source config before encryption).
- Mapping between entity and DTO uses extension methods (see below).

---

## Mapping (Entity ↔ DTO)

Extension methods in a `Mapping` static class next to the DTOs, or directly on the endpoint file
if trivial.

```csharp
public static class SourceMappings
{
    public static SourceResponse ToResponse(this Source source) => new(
        Id: source.Id,
        Name: source.Name,
        Type: source.Type.ToString().ToLowerInvariant(),
        CreatedAt: source.CreatedAt);
}
```

No AutoMapper. Explicit mapping only.

---

## Validation

Use `FluentValidation` or manual validation in the service layer.
Return `ValidationProblem` for invalid input.

```csharp
// Simple manual validation in service
public async Task<Source> CreateAsync(CreateSourceRequest request, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(request.Name))
        throw new ValidationException("Name is required");

    if (!Enum.TryParse<SourceType>(request.Type, true, out var sourceType))
        throw new ValidationException($"Invalid source type: {request.Type}");

    // ...
}
```

---

## Error Handling

Custom exception types mapped to HTTP status codes via a global exception handler middleware.

```csharp
// Infrastructure/Exceptions.cs
public class NotFoundException(string message) : Exception(message);
public class ValidationException(string message) : Exception(message);
public class ExternalServiceException(string message, Exception? inner = null) : Exception(message, inner);

// Infrastructure/ExceptionHandlerMiddleware.cs — or use IExceptionHandler (.NET 10)
public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        var (statusCode, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation Error"),
            ExternalServiceException => (StatusCodes.Status502BadGateway, "External Service Error"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message
        }, ct);
        return true;
    }
}
```

Rules:
- Services throw typed exceptions. Endpoints don't catch — the global handler does.
- Always return `ProblemDetails` for errors (RFC 9457).
- Log at `Warning` for 4xx, `Error` for 5xx — the handler does this, not the caller.

---

## Service Layer

One service class per domain. Injected via constructor. Services own the business logic;
endpoints are thin routing + mapping.

```csharp
public class SourceService(
    DashyDbContext db,
    IEncryptionService encryption,
    ILogger<SourceService> logger)
{
    public async Task<List<Source>> GetAllAsync(CancellationToken ct) { ... }
    public async Task<Source> CreateAsync(CreateSourceRequest request, CancellationToken ct) { ... }
    public async Task<Source> UpdateAsync(Guid id, UpdateSourceRequest request, CancellationToken ct) { ... }
    public async Task DeleteAsync(Guid id, CancellationToken ct) { ... }
    public async Task<TestConnectionResponse> TestConnectionAsync(Guid id, CancellationToken ct) { ... }
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

For real-time alert push. A single SSE endpoint that clients subscribe to.

```csharp
// Endpoints/AlertEndpoints.cs
group.MapGet("/stream", async (AlertSseService sse, HttpContext context, CancellationToken ct) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";

    await sse.StreamAsync(context.Response, ct);
});
```

The `AlertSseService` is a singleton that holds active connections and broadcasts events
when the background service detects a firing.

---

## Response Shape Conventions

| Operation | Status | Body |
|---|---|---|
| List | 200 | `T[]` |
| Get by ID | 200 | `T` |
| Create | 201 | `T` (with `Location` header) |
| Update | 200 | `T` |
| Delete | 204 | — |
| Action (test, query) | 200 | Result object |
| Validation error | 400 | `ProblemDetails` |
| Not found | 404 | `ProblemDetails` |
| External service failure | 502 | `ProblemDetails` |
