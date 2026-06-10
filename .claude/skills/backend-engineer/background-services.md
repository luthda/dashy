# Background Services

Reference for `BackgroundService` patterns, scheduled work, and the alert polling system.

---

## BackgroundService Base Pattern

Use `BackgroundService` with `PeriodicTimer` for recurring work. Create a scope per tick since
`BackgroundService` is a singleton but `DbContext` and business services are scoped.

```csharp
public class AlertPollingService(
    IServiceScopeFactory scopeFactory,
    ILogger<AlertPollingService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Alert polling service started, tick={Interval}s", TickInterval.TotalSeconds);

        using var timer = new PeriodicTimer(TickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Alert polling tick failed");
            }
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DashyDbContext>();
        var checker = scope.ServiceProvider.GetRequiredService<AlertCheckService>();

        var now = DateTime.UtcNow;

        // Load all enabled alerts, then filter due ones in memory.
        // SQLite can't translate `AddSeconds(column)` in a WHERE clause.
        var enabledAlerts = await db.Alerts
            .Include(a => a.Source)
            .Where(a => a.Enabled)
            .ToListAsync(ct);

        var dueAlerts = enabledAlerts
            .Where(a => a.LastCheckedAt is null
                || a.LastCheckedAt.Value.AddSeconds(a.CheckIntervalSeconds) <= now)
            .ToList();

        foreach (var alert in dueAlerts)
        {
            await checker.CheckAsync(alert, now, ct);
        }
    }
}
```

---

## AlertCheckService — Extracted for Testability

Per-alert check logic lives in a **separate scoped service** (`AlertCheckService`) so it can
be unit-tested with a real `DbContext` and fake infrastructure dependencies, without spinning
up the full background service.

```csharp
public class AlertCheckService(
    DashyDbContext db,
    SourceService sourceService,
    ILogSourceAdapterFactory adapterFactory,
    IAlertBroadcaster broadcaster,
    ILogger<AlertCheckService> logger)
{
    public async Task CheckAsync(Alert alert, DateTime now, CancellationToken ct)
    {
        try
        {
            var windowStart = now.AddSeconds(-alert.CheckIntervalSeconds);
            var adapter = adapterFactory.GetAdapter(alert.Source.Type);
            var configJson = sourceService.DecryptConfig(alert.Source);

            var resultCount = await adapter.CountAsync(new AdapterCountRequest(
                ConfigJson: configJson,
                SourceName: alert.Source.Name,
                BaseQuery: alert.Query,
                From: windowStart,
                To: now), ct);

            if (resultCount >= alert.Threshold)
            {
                db.AlertFirings.Add(new AlertFiring
                {
                    Id = Guid.NewGuid(),
                    AlertId = alert.Id,
                    FiredAt = now,
                    ResultCount = resultCount,
                });
                alert.Status = AlertStatus.Firing;
                alert.ResolvedAt = null;

                await broadcaster.BroadcastAsync(
                    new AlertFiredEvent(alert.Id, alert.Name, resultCount, now), ct);
            }
            else
            {
                alert.Status = AlertStatus.Ok;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Alert check failed: {AlertName}", alert.Name);
            alert.Status = AlertStatus.Error;
        }

        alert.LastCheckedAt = now;
        await db.SaveChangesAsync(ct);
    }
}
```

---

## Registration

```csharp
// Program.cs
builder.Services.AddScoped<AlertCheckService>();
builder.Services.AddSingleton<AlertSseService>();
builder.Services.AddSingleton<IAlertBroadcaster>(sp => sp.GetRequiredService<AlertSseService>());
builder.Services.AddHostedService<AlertPollingService>();
```

`AlertSseService` is registered both as itself (for the SSE endpoint to call `AddClient`/
`WritePingAsync`/`RemoveClient`) and as `IAlertBroadcaster` (for `AlertCheckService` to call
`BroadcastAsync`). Both resolve to the same singleton.

---

## Key Rules

1. **Always create a scope** — `BackgroundService` is a singleton; `DbContext` and services
   are scoped. Use `IServiceScopeFactory.CreateAsyncScope()` per tick.

2. **Catch per-iteration** — never let a single failed tick kill the service. Catch exceptions
   inside the loop, log at `Error`, and continue.

3. **Catch per-item** — `AlertCheckService.CheckAsync` catches and logs at `Warning` per alert,
   marks it `Error`, and continues. One broken alert must not block the rest.

4. **CancellationToken** — pass `stoppingToken` / `ct` everywhere. Let `OperationCanceledException`
   propagate to stop cleanly.

5. **In-memory due-filter** — SQLite cannot translate `column.AddSeconds(intColumn)` in a WHERE
   clause. Load all enabled alerts and filter due ones in application code.

6. **Extract check logic** — keep `BackgroundService` focused on scheduling; put per-item logic
   in a separate scoped service so it can be unit-tested directly.

---

## SSE Broadcasting

`AlertSseService` is a singleton with a per-client `SemaphoreSlim` write lock so heartbeat
pings and alert broadcasts never interleave on the same connection.

```csharp
public class AlertSseService(ILogger<AlertSseService> logger) : IAlertBroadcaster
{
    private readonly ConcurrentDictionary<string, SseClient> _clients = new();

    public string AddClient(HttpResponse response)
    {
        var id = Guid.NewGuid().ToString();
        _clients.TryAdd(id, new SseClient(response, new SemaphoreSlim(1, 1)));
        return id;
    }

    public void RemoveClient(string id) => _clients.TryRemove(id, out _);

    public async Task<bool> WritePingAsync(string id, CancellationToken ct)
    {
        if (!_clients.TryGetValue(id, out var client)) return false;
        return await TryWriteAsync(id, client, ": ping\n\n", ct);
    }

    public async Task BroadcastAsync(AlertFiredEvent evt, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(evt, JsonSerializerOptions.Web);
        var message = $"event: alert-fired\ndata: {json}\n\n";
        foreach (var (id, client) in _clients)
            await TryWriteAsync(id, client, message, ct);
    }

    private async Task<bool> TryWriteAsync(string id, SseClient client, string message, CancellationToken ct)
    {
        await client.WriteLock.WaitAsync(ct);
        try
        {
            await client.Response.WriteAsync(message, ct);
            await client.Response.Body.FlushAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "SSE write failed for client {Id}, removing", id);
            RemoveClient(id);
            return false;
        }
        finally
        {
            client.WriteLock.Release();
        }
    }

    private sealed record SseClient(HttpResponse Response, SemaphoreSlim WriteLock);
}
```

---

## Testing Background Services

Unit-test `AlertCheckService` directly with an in-memory SQLite `DashyDbContext` and fake
infrastructure (fake adapter, fake broadcaster, passthrough encryption). See `testing.md`.

Do not unit-test `AlertPollingService` itself — its only job is scoping and scheduling, which
is better exercised by integration tests against the running app.
