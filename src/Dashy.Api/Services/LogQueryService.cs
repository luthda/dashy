using System.Text.Json;
using Dashy.Api.Data;
using Dashy.Api.Infrastructure;
using Dashy.Api.Models;
using Microsoft.EntityFrameworkCore;
using LogLevel = Dashy.Api.Models.LogLevel;

namespace Dashy.Api.Services;

public class LogQueryService(
    DashyDbContext db,
    SourceService sourceService,
    ILogSourceAdapterFactory adapterFactory,
    ILogger<LogQueryService> logger)
{
    private const int DefaultLimit = 500;

    public async Task<List<LogEntry>> QueryAsync(LogQueryRequest request, CancellationToken ct)
    {
        logger.LogDebug("Log query for source {SourceId}", request.SourceId);

        var source = await db.Sources.FindAsync([request.SourceId], ct)
            ?? throw new SourceNotFoundException(request.SourceId);

        var tagFilters = await ResolveTagFiltersAsync(request.TagIds ?? [], ct);
        var adapter = adapterFactory.GetAdapter(source.Type);
        var configJson = sourceService.DecryptConfig(source);

        var adapterRequest = new AdapterQueryRequest(
            ConfigJson: configJson,
            SourceName: source.Name,
            FreeText: request.Query,
            TimeRange: request.TimeRange,
            TagFilters: tagFilters,
            Limit: request.Limit ?? DefaultLimit);

        return await adapter.QueryAsync(adapterRequest, ct);
    }

    private async Task<TagFilters> ResolveTagFiltersAsync(List<Guid> tagIds, CancellationToken ct)
    {
        if (tagIds.Count == 0) return TagFilters.Empty;

        var tags = await db.Tags
            .Where(t => tagIds.Contains(t.Id))
            .ToListAsync(ct);

        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var levels = new HashSet<LogLevel>();
        var eventTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            try
            {
                var filters = JsonSerializer.Deserialize<TagFilterJson>(tag.Filters);
                if (filters is null) continue;

                foreach (var t in filters.Terms ?? []) terms.Add(t);
                foreach (var l in filters.Levels ?? []) levels.Add(l);
                foreach (var e in filters.EventTypes ?? []) eventTypes.Add(e);
            }
            catch { /* malformed tag filter — skip */ }
        }

        return new TagFilters(terms.ToList(), levels.ToList(), eventTypes.ToList());
    }
}

// ── Request / internal types ─────────────────────────────────────────────────

public record LogQueryRequest(
    Guid SourceId,
    string? Query,
    List<Guid>? TagIds,
    TimeRangeRequest? TimeRange,
    int? Limit);

public record TimeRangeRequest(
    string Type,
    string? Value,
    DateTime? From,
    DateTime? To);

public record TagFilters(
    List<string> Terms,
    List<LogLevel> Levels,
    List<string> EventTypes)
{
    public static TagFilters Empty => new([], [], []);
}

internal record TagFilterJson(
    List<string>? Terms,
    List<LogLevel>? Levels,
    List<string>? EventTypes);

public class SourceNotFoundException(Guid id)
    : Exception($"Source {id} was not found");
