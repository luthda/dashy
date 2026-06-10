using Dashy.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dashy.Api.Application.Services;

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

        var enabledAlerts = await db.Alerts
            .Include(a => a.Source)
            .Where(a => a.Enabled)
            .ToListAsync(ct);

        // Due-filter in memory: SQLite can't translate AddSeconds with a column
        // argument, and the alerts table is tiny.
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
