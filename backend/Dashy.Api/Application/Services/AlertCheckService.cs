using Dashy.Api.Application.Abstractions;
using Dashy.Api.Domain.Entities;
using Dashy.Api.Infrastructure.Persistence;

namespace Dashy.Api.Application.Services;

/// <summary>
/// Production poll logic for a single alert: runs the stored count query over
/// the window since the previous check, updates status / firing history, and
/// broadcasts firings. Called by <see cref="AlertPollingService"/> on every
/// tick — kept as a plain scoped class (rather than logic inside the
/// BackgroundService) so it can be resolved per tick and tested in isolation.
/// </summary>
public class AlertCheckService(
    DashyDbContext db,
    SourceService sourceService,
    ILogSourceAdapterFactory adapterFactory,
    IAlertBroadcaster broadcaster,
    ILogger<AlertCheckService> logger)
{
    public async Task CheckAsync(Alert alert, DateTime now, CancellationToken ct)
    {
        AlertFiredEvent? fired = null;

        try
        {
            // Windows tile across polls: (LastCheckedAt, now], so each entry is
            // counted exactly once and — after "Resolved" — old logs cannot
            // re-trigger the alert; only new ones can. First check falls back to
            // one tick interval.
            var windowStart = alert.LastCheckedAt
                ?? now.AddSeconds(-AlertPollingService.TickIntervalSeconds);

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

                logger.LogInformation("Alert fired: {AlertName} count={Count}", alert.Name, resultCount);
                fired = new AlertFiredEvent(alert.Id, alert.Name, resultCount, now);
            }
            else
            {
                // Clean poll: back to Ok, but ResolvedAt is left untouched — it is
                // only cleared by a new firing above.
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

        // Broadcast only AFTER the commit: the frontend reacts to the SSE event
        // by refetching /alerts immediately, and that read must observe the new
        // Firing status — broadcasting earlier races the refetch against the
        // transaction and the bell dot is randomly missed.
        if (fired is not null)
        {
            await broadcaster.BroadcastAsync(fired, ct);
        }
    }
}
