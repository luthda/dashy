using System.Text.Json;
using Dashy.Api.Application.Abstractions;
using Dashy.Api.Application.Exceptions;
using Dashy.Api.Domain.Models;
using Dashy.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using LogLevel = Dashy.Api.Domain.Models.LogLevel;

namespace Dashy.Api.Application.Services;

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
            Limit: request.Limit ?? DefaultLimit,
            EventTypes: request.EventTypes,
            Skip: request.Skip);

        try
        {
            return await adapter.QueryAsync(adapterRequest, ct);
        }
        catch (Exception ex) when (ex is not SourceNotFoundException)
        {
            var (statusCode, body) = ExtractErrorDetails(ex);
            throw new LogSourceQueryException(statusCode, body, ex);
        }
    }

    private async Task<TagFilters> ResolveTagFiltersAsync(List<Guid> tagIds, CancellationToken ct)
    {
        if (tagIds.Count == 0)
        {
            return TagFilters.Empty;
        }

        var tags = await db.Tags
            .Where(t => tagIds.Contains(t.Id))
            .ToListAsync(ct);

        var termGroups = new List<List<string>>();
        var levels = new HashSet<LogLevel>();
        var eventTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            try
            {
                var filters = JsonSerializer.Deserialize<TagFilterJson>(tag.Filters);
                if (filters is null)
                {
                    continue;
                }

                var tagTerms = (filters.Terms ?? []).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
                if (tagTerms.Count > 0)
                {
                    termGroups.Add(tagTerms);
                }

                foreach (var l in filters.Levels ?? [])
                {
                    levels.Add(l);
                }

                foreach (var e in filters.EventTypes ?? [])
                {
                    eventTypes.Add(e);
                }
            }
            catch { /* malformed tag filter — skip */ }
        }

        return new TagFilters(termGroups, levels.ToList(), eventTypes.ToList());
    }

    private static (int StatusCode, string Body) ExtractErrorDetails(Exception ex)
    {
        var statusCode = ex.GetType().GetProperty("StatusCode")?.GetValue(ex) as int? ?? 500;
        var body = ex.GetType().GetProperty("Body")?.GetValue(ex) as string ?? ex.Message;
        return (statusCode, body);
    }
}

// ── Request / internal types ─────────────────────────────────────────────────

public record LogQueryRequest(
    Guid SourceId,
    string? Query,
    List<Guid>? TagIds,
    TimeRangeRequest? TimeRange,
    int? Limit,
    List<string>? EventTypes = null,
    int? Skip = null);

public record TimeRangeRequest(
    string Type,
    string? Value,
    DateTime? From,
    DateTime? To);

public record TagFilters(
    List<List<string>> TermGroups,
    List<LogLevel> Levels,
    List<string> EventTypes)
{
    public static TagFilters Empty => new([], [], []);
}

internal record TagFilterJson(
    List<string>? Terms,
    List<LogLevel>? Levels,
    List<string>? EventTypes);
