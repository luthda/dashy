# Background Services

Reference for `IHostedService` / `BackgroundService` patterns, scheduled work, and the alert
polling system.

---

## BackgroundService Base Pattern

Use `BackgroundService` (which implements `IHostedService`) with `PeriodicTimer` for
recurring work. Create a scope per iteration since `BackgroundService` is a singleton.

```csharp
public class AlertPollingService(
    IServiceScopeFactory scopeFactory,
    AlertSseService sseService,
    ILogger<AlertPollingService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

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
        var queryService = scope.ServiceProvider.GetRequiredService<LogQueryService>();

        var dueAlerts = await db.Alerts
            .Include(a => a.Source)
            .Where(a => a.Enabled)
            .Where(a => a.LastCheckedAt == null
                || a.LastCheckedAt.Value.AddSeconds(a.CheckIntervalSeconds) <= DateTimeOffset.UtcNow)
            .ToListAsync(ct);

        foreach (var alert in dueAlerts)
        {
            await CheckAlertAsync(alert, db, queryService, ct);
        }
    }

    private async Task CheckAlertAsync(
        Alert alert, DashyDbContext db, LogQueryService queryService, CancellationToken ct)
    {
        try
        {
            var resultCount = await queryService.CountAsync(alert.Source, alert.Query, ct);

            if (resultCount >= alert.Threshold)
            {
                var firing = new AlertFiring
                {
                    AlertId = alert.Id,
                    FiredAt = DateTimeOffset.UtcNow,
                    ResultCount = resultCount
                };
                db.AlertFirings.Add(firing);
                alert.Status = AlertStatus.Firing;

                await sseService.BroadcastAsync(new AlertFiredEvent(alert.Id, alert.Name, resultCount));
                logger.LogInformation("Alert fired: {AlertName} count={Count}", alert.Name, resultCount);
            }
            else
            {
                alert.Status = AlertStatus.Ok;
            }

            alert.LastCheckedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Alert check failed: {AlertName}", alert.Name);
            alert.Status = AlertStatus.Error;
            alert.LastCheckedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
```

---

## Registration

```csharp
// Program.cs
builder.Services.AddHostedService<AlertPollingService>();
```

---

## Key Rules

1. **Always create a scope** — `BackgroundService` is a singleton, but `DbContext` and services
   are scoped. Use `IServiceScopeFactory.CreateAsyncScope()` per iteration.

2. **Catch per-iteration** — never let a single failed tick kill the service. Catch exceptions
   inside the loop, log them, and continue.

3. **CancellationToken** — respect `stoppingToken` everywhere. Use it in all async calls.
   Let `OperationCanceledException` propagate to stop the service cleanly.

4. **Logging** — log start/stop at `Information`, each tick failure at `Error`, per-item
   failures at `Warning`.

5. **Timer interval** — the `PeriodicTimer` tick defines the minimum resolution. Individual
   alerts define their own `check_interval_seconds`; the polling loop skips alerts that aren't
   due yet.

---

## SSE Broadcasting

`AlertSseService` is a singleton that manages active SSE connections and broadcasts events.

```csharp
public class AlertSseService
{
    private readonly ConcurrentDictionary<string, StreamWriter> _clients = new();

    public async Task StreamAsync(HttpResponse response, CancellationToken ct)
    {
        var clientId = Guid.NewGuid().ToString();
        var writer = new StreamWriter(response.Body) { AutoFlush = true };
        _clients.TryAdd(clientId, writer);

        try
        {
            await Task.Delay(Timeout.Infinite, ct);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
        }
        finally
        {
            _clients.TryRemove(clientId, out _);
        }
    }

    public async Task BroadcastAsync<T>(T data)
    {
        var json = JsonSerializer.Serialize(data);
        var message = $"data: {json}\n\n";

        foreach (var (id, writer) in _clients)
        {
            try
            {
                await writer.WriteAsync(message);
            }
            catch
            {
                _clients.TryRemove(id, out _);
            }
        }
    }
}
```

Registration:

```csharp
builder.Services.AddSingleton<AlertSseService>();
```

---

## Testing Background Services

See `testing.md` for integration testing patterns. For unit testing a background service,
inject a mock `IServiceScopeFactory` or test the `PollAsync` / `CheckAlertAsync` logic
directly by extracting it into a testable service class.
