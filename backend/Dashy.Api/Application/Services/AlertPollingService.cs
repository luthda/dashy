using Dashy.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dashy.Api.Application.Services;

public class AlertPollingService(
    IServiceScopeFactory scopeFactory,
    ILogger<AlertPollingService> logger) : BackgroundService
{
    /// <summary>Every enabled alert is checked once per tick — same cadence as the frontend's live mode.</summary>
    public const int TickIntervalSeconds = 60;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(TickIntervalSeconds);

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

        foreach (var alert in enabledAlerts)
        {
            try
            {
                await checker.CheckAsync(alert, now, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Isolate alerts from each other: a failed save for one must
                // not skip the remaining checks this tick.
                logger.LogError(ex, "Alert check crashed: {AlertName}", alert.Name);
            }
        }
    }
}
