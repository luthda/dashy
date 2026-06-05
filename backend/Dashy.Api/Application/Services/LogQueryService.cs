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

        var adapter = adapterFactory.GetAdapter(source.Type);
        var configJson = sourceService.DecryptConfig(source);
        var limit = request.Limit ?? DefaultLimit;

        var perTagFilters = await ResolvePerTagFiltersAsync(request.TagIds ?? [], ct);

        if (perTagFilters.Count <= 1)
        {
            var tagFilters = perTagFilters.Count == 1 ? perTagFilters[0] : TagFilters.Empty;
            return await ExecuteAdapterQueryAsync(adapter, configJson, source.Name, request, tagFilters, limit, ct);
        }

        // Multiple tags — run one query per tag in parallel, merge results
        var tasks = perTagFilters.Select(tf =>
            ExecuteAdapterQueryAsync(adapter, configJson, source.Name, request, tf, limit, ct));

        var results = await Task.WhenAll(tasks);

        return results
            .SelectMany(r => r)
            .DistinctBy(e => (e.Timestamp, e.EventType, e.Message, e.Source))
            .OrderByDescending(e => e.Timestamp)
            .ToList();
    }

    private async Task<List<LogEntry>> ExecuteAdapterQueryAsync(
        ILogSourceAdapter adapter, string configJson, string sourceName,
        LogQueryRequest request, TagFilters tagFilters, int limit, CancellationToken ct)
    {
        var adapterRequest = new AdapterQueryRequest(
            ConfigJson: configJson,
            SourceName: sourceName,
            FreeText: request.Query,
            TimeRange: request.TimeRange,
            TagFilters: tagFilters,
            Limit: limit,
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

    private async Task<List<TagFilters>> ResolvePerTagFiltersAsync(List<Guid> tagIds, CancellationToken ct)
    {
        if (tagIds.Count == 0)
        {
            return [];
        }

        var tags = await db.Tags
            .Where(t => tagIds.Contains(t.Id))
            .ToListAsync(ct);

        var result = new List<TagFilters>();

        foreach (var tag in tags)
        {
            try
            {
                var filters = JsonSerializer.Deserialize<TagFilterJson>(tag.Filters);
                if (filters is null)
                {
                    continue;
                }

                var terms = (filters.Terms ?? []).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
                var termGroups = terms.Count > 0 ? new List<List<string>> { terms } : [];
                var levels = (filters.Levels ?? []).ToList();
                var eventTypes = (filters.EventTypes ?? []).ToList();

                result.Add(new TagFilters(termGroups, levels, eventTypes));
            }
            catch { /* malformed tag filter — skip */ }
        }

        return result;
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
