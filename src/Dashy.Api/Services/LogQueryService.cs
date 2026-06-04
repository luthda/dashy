using System.Text;
using System.Text.Json;
using Dashy.Api.Data;
using Dashy.Api.Data.Entities;
using Dashy.Api.Infrastructure;
using Dashy.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Dashy.Api.Services;

public class LogQueryService(
    DashyDbContext db,
    SourceService sourceService,
    AppInsightsClient appInsights,
    LokiClient loki,
    ILogger<LogQueryService> logger)
{
    private const int DefaultLimit = 500;

    public async Task<List<LogEntry>> QueryAsync(LogQueryRequest request, CancellationToken ct)
    {
        logger.LogDebug("Log query for source {SourceId}", request.SourceId);

        var source = await db.Sources.FindAsync([request.SourceId], ct)
            ?? throw new SourceNotFoundException(request.SourceId);

        // Resolve tags to merge their filters into the query
        var tagFilters = await ResolveTagFiltersAsync(request.TagIds ?? [], ct);
        var limit = request.Limit ?? DefaultLimit;

        return source.Type switch
        {
            SourceType.AppInsights => await QueryAppInsightsAsync(source, request, tagFilters, limit, ct),
            SourceType.Loki        => await QueryLokiAsync(source, request, tagFilters, limit, ct),
            _                      => throw new InvalidOperationException($"Unknown source type: {source.Type}"),
        };
    }

    // ── App Insights ──────────────────────────────────────────────────────────

    private async Task<List<LogEntry>> QueryAppInsightsAsync(
        Source source,
        LogQueryRequest request,
        TagFilters tagFilters,
        int limit,
        CancellationToken ct)
    {
        var cfg = sourceService.DecryptConfig<AppInsightsConfig>(source);
        var kql = BuildKql(request.Query, request.TimeRange, tagFilters, limit);
        var timespan = ToAppInsightsTimespan(request.TimeRange);

        logger.LogDebug("App Insights KQL: {Kql}", kql);

        return await appInsights.QueryAsync(cfg.AppId, cfg.ApiKey, kql, timespan, source.Name, ct);
    }

    public static string BuildKql(
        string? freeText,
        TimeRangeRequest? timeRange,
        TagFilters tags,
        int limit)
    {
        var sb = new StringBuilder("traces");

        var clauses = new List<string>();

        // Free text
        if (!string.IsNullOrWhiteSpace(freeText))
            clauses.Add($"message contains \"{EscapeKql(freeText)}\"");

        // Level filter from tags
        if (tags.Levels.Count > 0)
        {
            var levelInts = tags.Levels
                .Select(LevelToSeverityInt)
                .Where(v => v >= 0)
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            if (levelInts.Count > 0)
            {
                var joined = string.Join(", ", levelInts);
                clauses.Add($"severityLevel in ({joined})");
            }
        }

        // EventType filter from tags
        foreach (var et in tags.EventTypes)
            clauses.Add($"customDimensions[\"EventType\"] == \"{EscapeKql(et)}\"");

        // Extra term filters from tags
        foreach (var term in tags.Terms)
            clauses.Add($"message contains \"{EscapeKql(term)}\"");

        // Absolute time range in the query itself (timespan parameter covers relative)
        if (timeRange?.Type == "absolute" && timeRange.From.HasValue && timeRange.To.HasValue)
        {
            clauses.Add($"timestamp >= datetime({timeRange.From.Value:O})");
            clauses.Add($"timestamp <= datetime({timeRange.To.Value:O})");
        }

        if (clauses.Count > 0)
            sb.Append("\n| where ").Append(string.Join("\n    and ", clauses));

        sb.Append("\n| order by timestamp desc");
        sb.Append($"\n| limit {limit}");

        return sb.ToString();
    }

    private static string? ToAppInsightsTimespan(TimeRangeRequest? timeRange)
    {
        if (timeRange?.Type != "relative") return null;
        return timeRange.Value switch
        {
            "15m" => "PT15M",
            "1h"  => "PT1H",
            "6h"  => "PT6H",
            "24h" => "P1D",
            "7d"  => "P7D",
            _     => "PT1H",
        };
    }

    // ── Loki ──────────────────────────────────────────────────────────────────

    private async Task<List<LogEntry>> QueryLokiAsync(
        Source source,
        LogQueryRequest request,
        TagFilters tagFilters,
        int limit,
        CancellationToken ct)
    {
        var cfg = sourceService.DecryptConfig<LokiConfig>(source);
        var (start, end) = ToLokiTimeRange(request.TimeRange);
        var logql = BuildLogQL(request.Query, tagFilters);

        logger.LogDebug("Loki LogQL: {LogQL}", logql);

        return await loki.QueryAsync(cfg.BaseUrl, cfg.OrgId, cfg.AuthToken, logql, start, end, limit, source.Name, ct);
    }

    public static string BuildLogQL(string? freeText, TagFilters tags)
    {
        // Start with a broad selector that matches all streams
        var sb = new StringBuilder("{job=~\".+\"}");

        if (!string.IsNullOrWhiteSpace(freeText))
            sb.Append($" |= \"{EscapeLogQL(freeText)}\"");

        foreach (var term in tags.Terms)
            sb.Append($" |= \"{EscapeLogQL(term)}\"");

        foreach (var level in tags.Levels)
            sb.Append($" | level=\"{level}\"");

        foreach (var et in tags.EventTypes)
            sb.Append($" | json | EventType=\"{EscapeLogQL(et)}\"");

        return sb.ToString();
    }

    private static (string start, string end) ToLokiTimeRange(TimeRangeRequest? timeRange)
    {
        var now = DateTimeOffset.UtcNow;
        var to  = now;
        var from = timeRange?.Type switch
        {
            "relative" => timeRange.Value switch
            {
                "15m" => now.AddMinutes(-15),
                "6h"  => now.AddHours(-6),
                "24h" => now.AddHours(-24),
                "7d"  => now.AddDays(-7),
                _     => now.AddHours(-1),
            },
            "absolute" when timeRange.From.HasValue && timeRange.To.HasValue
                => new DateTimeOffset(timeRange.From.Value, TimeSpan.Zero),
            _ => now.AddHours(-1),
        };

        if (timeRange?.Type == "absolute" && timeRange.To.HasValue)
            to = new DateTimeOffset(timeRange.To.Value, TimeSpan.Zero);

        return (
            (from.ToUnixTimeSeconds() * 1_000_000_000L).ToString(),
            (to.ToUnixTimeSeconds()   * 1_000_000_000L).ToString()
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<TagFilters> ResolveTagFiltersAsync(List<Guid> tagIds, CancellationToken ct)
    {
        if (tagIds.Count == 0) return TagFilters.Empty;

        var tags = await db.Tags
            .Where(t => tagIds.Contains(t.Id))
            .ToListAsync(ct);

        var terms      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var levels     = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var eventTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in tags)
        {
            try
            {
                var filters = JsonSerializer.Deserialize<TagFilterJson>(tag.Filters);
                if (filters is null) continue;

                foreach (var t in filters.Terms      ?? []) terms.Add(t);
                foreach (var l in filters.Levels     ?? []) levels.Add(l);
                foreach (var e in filters.EventTypes ?? []) eventTypes.Add(e);
            }
            catch { /* malformed tag filter — skip */ }
        }

        return new TagFilters(terms.ToList(), levels.ToList(), eventTypes.ToList());
    }

    private static string EscapeKql(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string EscapeLogQL(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static int LevelToSeverityInt(string level) => level.ToLowerInvariant() switch
    {
        "trace" => 0,
        "info"  => 1,
        "warn"  => 2,
        "error" => 3,
        _       => -1,
    };
}

// ── Request / internal types ─────────────────────────────────────────────────

public record LogQueryRequest(
    Guid SourceId,
    string? Query,
    List<Guid>? TagIds,
    TimeRangeRequest? TimeRange,
    int? Limit);

public record TimeRangeRequest(
    string Type,              // "relative" | "absolute"
    string? Value,            // "15m" | "1h" | "6h" | "24h" | "7d"
    DateTime? From,
    DateTime? To);

public record TagFilters(
    List<string> Terms,
    List<string> Levels,
    List<string> EventTypes)
{
    public static TagFilters Empty => new([], [], []);
}

internal record TagFilterJson(
    List<string>? Terms,
    List<string>? Levels,
    List<string>? EventTypes);

public class SourceNotFoundException(Guid id)
    : Exception($"Source {id} was not found");
