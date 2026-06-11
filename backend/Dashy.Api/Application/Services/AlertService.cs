using Dashy.Api.Application.Abstractions;
using Dashy.Api.Application.Exceptions;
using Dashy.Api.Domain.Entities;
using Dashy.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dashy.Api.Application.Services;

public class AlertService(
    DashyDbContext db,
    ILogSourceAdapterFactory adapterFactory,
    ILogger<AlertService> logger)
{
    public const int FiringHistoryLimit = 50;

    public async Task<List<Alert>> GetAllAsync(CancellationToken ct)
    {
        logger.LogDebug("Listing alerts");

        return await db.Alerts
            .AsNoTracking()
            .Include(a => a.Source)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Alert> CreateAsync(CreateAlertRequest request, CancellationToken ct)
    {
        logger.LogInformation("Creating alert {Name}", request.Name);

        var source = await db.Sources.FindAsync([request.SourceId], ct)
            ?? throw new SourceNotFoundException(request.SourceId);

        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            SourceId = source.Id,
            Query = await ResolveQueryAsync(request.Query, request.TagId, source, ct),
            Threshold = request.Threshold,
            Enabled = request.Enabled,
            CreatedAt = DateTime.UtcNow,
        };

        db.Alerts.Add(alert);
        await db.SaveChangesAsync(ct);

        alert.Source = source;
        return alert;
    }

    public async Task<Alert?> UpdateAsync(Guid id, UpdateAlertRequest request, CancellationToken ct)
    {
        logger.LogInformation("Updating alert {Id}", id);

        var alert = await db.Alerts
            .Include(a => a.Source)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null)
        {
            return null;
        }

        var source = alert.Source;
        if (request.SourceId != alert.SourceId)
        {
            source = await db.Sources.FindAsync([request.SourceId], ct)
                ?? throw new SourceNotFoundException(request.SourceId);
            alert.SourceId = source.Id;
            alert.Source = source;
        }

        if (request.Enabled && !alert.Enabled)
        {
            // Re-enable starts a fresh poll window — the check window is
            // (LastCheckedAt, now], so a stale value from before the disabled
            // period would let old events retrigger the alert immediately.
            alert.LastCheckedAt = null;
        }

        alert.Name = request.Name;
        alert.Query = await ResolveQueryAsync(request.Query, request.TagId, source, ct);
        alert.Threshold = request.Threshold;
        alert.Enabled = request.Enabled;

        await db.SaveChangesAsync(ct);
        return alert;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        logger.LogInformation("Deleting alert {Id}", id);

        var alert = await db.Alerts.FindAsync([id], ct);
        if (alert is null)
        {
            return false;
        }

        db.Alerts.Remove(alert);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Alert?> ResolveAsync(Guid id, CancellationToken ct)
    {
        logger.LogInformation("Resolving alert {Id}", id);

        var alert = await db.Alerts
            .Include(a => a.Source)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null)
        {
            return null;
        }

        alert.Status = AlertStatus.Ok;
        alert.ResolvedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return alert;
    }

    public async Task<List<AlertFiring>?> GetFiringsAsync(Guid id, CancellationToken ct)
    {
        logger.LogDebug("Listing firings for alert {Id}", id);

        var exists = await db.Alerts.AnyAsync(a => a.Id == id, ct);
        if (!exists)
        {
            return null;
        }

        return await db.AlertFirings
            .AsNoTracking()
            .Where(f => f.AlertId == id)
            .OrderByDescending(f => f.FiredAt)
            .Take(FiringHistoryLimit)
            .ToListAsync(ct);
    }

    // When tagId is present it wins over query: the tag's filters are flattened
    // into base KQL at save time, so the alert stays stable if the tag changes later.
    private async Task<string> ResolveQueryAsync(string? query, Guid? tagId, Source source, CancellationToken ct)
    {
        if (tagId is null)
        {
            return query ?? "";
        }

        var tag = await db.Tags.FindAsync([tagId.Value], ct)
            ?? throw new TagNotFoundException(tagId.Value);

        return BuildQueryFromTag(tag, source);
    }

    private string BuildQueryFromTag(Tag tag, Source source)
    {
        var filters = TagFilters.FromJson(tag.Filters) ?? TagFilters.Empty;

        // The adapter owns query syntax; one that cannot back alerts throws
        // UnsupportedAlertSourceException (mapped to 422 by the endpoint).
        return adapterFactory.GetAdapter(source.Type).BuildAlertQuery(filters);
    }
}

// ── Request DTOs ─────────────────────────────────────────────────────────────

public record CreateAlertRequest(
    string Name,
    Guid SourceId,
    string? Query,
    Guid? TagId,
    int Threshold = 1,
    bool Enabled = true);

public record UpdateAlertRequest(
    string Name,
    Guid SourceId,
    string? Query,
    Guid? TagId,
    int Threshold = 1,
    bool Enabled = true);
