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

    public async Task<LogQueryResult> QueryAsync(LogQueryRequest request, CancellationToken ct)
    {
        logger.LogDebug("Log query for source {SourceId}", request.SourceId);

        var source = await db.Sources.FindAsync([request.SourceId], ct)
            ?? throw new SourceNotFoundException(request.SourceId);

        var adapter = adapterFactory.GetAdapter(source.Type);
        var configJson = sourceService.DecryptConfig(source);
        var limit = request.Limit ?? DefaultLimit;
        var skip = request.Skip ?? 0;

        var perTagFilters = await ResolvePerTagFiltersAsync(request.TagIds ?? [], ct);

        if (perTagFilters.Count <= 1)
        {
            var tagFilters = perTagFilters.Count == 1 ? perTagFilters[0] : TagFilters.Empty;
            // Probe one extra row beyond the page to detect whether a next page exists.
            var page = await ExecuteAdapterQueryAsync(
                adapter, configJson, source.Name, request, tagFilters, limit + 1, skip, ct);
            return Paginate(page, limit);
        }

        // Multiple tags — run one query per tag in parallel, then merge. Each
        // sub-query fetches the newest (skip + limit + 1) rows from offset 0: the
        // merged top-(skip + limit + 1) can't contain more than that many rows from
        // any single tag, so this is enough to compute the global window — plus the
        // one probe row — correctly. Paging is applied once, on the merged stream
        // below; passing request.Skip into each sub-query would skip independently
        // and return the wrong page.
        var fetch = skip + limit + 1;
        var tasks = perTagFilters.Select(tf =>
            ExecuteAdapterQueryAsync(adapter, configJson, source.Name, request, tf, fetch, skip: 0, ct));

        var results = await Task.WhenAll(tasks);

        var merged = results
            .SelectMany(r => r)
            .DistinctBy(e => (e.Timestamp, e.EventType, e.Message, e.Source))
            .OrderByDescending(e => e.Timestamp)
            .Skip(skip)
            .Take(limit + 1)
            .ToList();

        return Paginate(merged, limit);
    }

    // Trims the one-row probe and reports whether more pages follow.
    private static LogQueryResult Paginate(List<LogEntry> rows, int limit)
    {
        var hasMore = rows.Count > limit;
        var entries = hasMore ? rows.Take(limit).ToList() : rows;
        return new LogQueryResult(entries, hasMore);
    }

    private async Task<List<LogEntry>> ExecuteAdapterQueryAsync(
        ILogSourceAdapter adapter, string configJson, string sourceName,
        LogQueryRequest request, TagFilters tagFilters, int limit, int skip, CancellationToken ct)
    {
        var adapterRequest = new AdapterQueryRequest(
            ConfigJson: configJson,
            SourceName: sourceName,
            FreeText: request.Query,
            TimeRange: request.TimeRange,
            TagFilters: tagFilters,
            Limit: limit,
            EventTypes: request.EventTypes,
            Skip: skip);

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
            var filters = TagFilters.FromJson(tag.Filters);
            if (filters is not null)
            {
                result.Add(filters);
            }
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

public record LogQueryResult(List<LogEntry> Entries, bool HasMore);

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

    /// <summary>Parses a tag's serialized filters JSON. Returns null when malformed.</summary>
    public static TagFilters? FromJson(string json)
    {
        try
        {
            var filters = JsonSerializer.Deserialize<TagFilterJson>(json);
            if (filters is null)
            {
                return null;
            }

            var terms = (filters.Terms ?? []).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            var termGroups = terms.Count > 0 ? new List<List<string>> { terms } : [];
            var levels = (filters.Levels ?? []).ToList();
            var eventTypes = (filters.EventTypes ?? []).ToList();

            return new TagFilters(termGroups, levels, eventTypes);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal record TagFilterJson(
    List<string>? Terms,
    List<LogLevel>? Levels,
    List<string>? EventTypes);
