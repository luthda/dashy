using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dashy.Api.Application.Abstractions;
using Dashy.Api.Application.Services;
using Dashy.Api.Domain.Entities;
using Dashy.Api.Domain.Models;
using Models = Dashy.Api.Domain.Models;
using LogLevel = Dashy.Api.Domain.Models.LogLevel;

namespace Dashy.Api.Infrastructure.LogSources;

public sealed class AppInsightsAdapter(HttpClient http, ILogger<AppInsightsAdapter> logger) : ILogSourceAdapter
{
    private const string BaseUrl = "https://api.applicationinsights.io/v1/apps";

    public SourceType SourceType => SourceType.AppInsights;

    public async Task<List<LogEntry>> QueryAsync(AdapterQueryRequest request, CancellationToken ct)
    {
        var cfg = JsonSerializer.Deserialize<AppInsightsConfig>(request.ConfigJson, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Invalid App Insights config");

        var kql = BuildKql(request.FreeText, request.TimeRange, request.TagFilters, request.Limit, request.EventTypes, request.Skip ?? 0);
        var timespan = ToAppInsightsTimespan(request.TimeRange);

        logger.LogInformation(
            "App Insights query for {Source} (eventTypes=[{EventTypes}], timespan={Timespan}):\n{Kql}",
            request.SourceName,
            request.EventTypes is { Count: > 0 } ? string.Join(",", request.EventTypes) : "all",
            timespan ?? "(none)",
            kql);

        return await ExecuteQueryAsync(cfg.AppId, cfg.ApiKey, kql, timespan, request.SourceName, ct);
    }

    public async Task TestConnectionAsync(string configJson, string sourceName, CancellationToken ct)
    {
        var cfg = JsonSerializer.Deserialize<AppInsightsConfig>(configJson, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Invalid App Insights config");

        await ExecuteQueryAsync(cfg.AppId, cfg.ApiKey,
            "union (traces | extend eventType = \"trace\", eventMessage = message) | project timestamp, eventType, eventMessage | limit 1",
            null, sourceName, ct);
    }

    public async Task<int> CountAsync(AdapterCountRequest request, CancellationToken ct)
    {
        var cfg = JsonSerializer.Deserialize<AppInsightsConfig>(request.ConfigJson, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Invalid App Insights config");

        var kql = BuildCountKql(request.BaseQuery, request.From, request.To);

        logger.LogInformation("App Insights count query for {Source}:\n{Kql}", request.SourceName, kql);

        var result = await ExecuteRawQueryAsync(cfg.AppId, cfg.ApiKey, kql, timespan: null, ct);
        return ParseCount(result);
    }

    private async Task<List<LogEntry>> ExecuteQueryAsync(
        string appId, string apiKey, string kql, string? timespan,
        string sourceName, CancellationToken ct)
    {
        var result = await ExecuteRawQueryAsync(appId, apiKey, kql, timespan, ct);

        var entries = MapToLogEntries(result, sourceName);

        var rawRows = result.Tables is [var tbl, ..] ? tbl.Rows.Count : 0;
        var breakdown = entries.Count == 0
            ? "(none)"
            : string.Join(", ", entries
                .GroupBy(e => e.EventType ?? "(null)")
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key}={g.Count()}"));
        logger.LogInformation(
            "App Insights {Source}: {RawRows} raw rows → {Mapped} entries [{Breakdown}]",
            sourceName, rawRows, entries.Count, breakdown);

        return entries;
    }

    private async Task<AppInsightsQueryResult> ExecuteRawQueryAsync(
        string appId, string apiKey, string kql, string? timespan, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{appId}/query");
        request.Headers.Add("X-Api-Key", apiKey);
        request.Content = JsonContent.Create(new { query = kql, timespan });

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        using var response = await http.SendAsync(request, ct);
        stopwatch.Stop();

        logger.LogInformation("App Insights responded {StatusCode} in {ElapsedMs} ms",
            (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("App Insights query failed {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new AppInsightsQueryException((int)response.StatusCode, body);
        }

        return await response.Content
            .ReadFromJsonAsync<AppInsightsQueryResult>(ct)
            ?? throw new InvalidOperationException("Empty response from App Insights");
    }

    private static int ParseCount(AppInsightsQueryResult result)
    {
        if (result.Tables is not [var table, ..] || table.Rows is not [var row, ..] || row.Length == 0)
        {
            return 0;
        }

        var el = row[0];
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
        {
            return n;
        }

        return int.TryParse(el.GetRawText().Trim('"'), out var parsed) ? parsed : 0;
    }

    // ── KQL builder ──────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> TableProjections = new()
    {
        ["traces"] = "(traces | extend eventType = \"trace\", eventMessage = message)",
        ["requests"] = "(requests | extend eventType = \"request\", eventMessage = strcat(name, \" \", resultCode, \" \", duration, \"ms\"))",
        ["dependencies"] = "(dependencies | extend eventType = \"dependency\", eventMessage = strcat(name, \" \", target, \" \", duration, \"ms \", \"success=\", success))",
        ["exceptions"] = "(exceptions | extend eventType = \"exception\", eventMessage = strcat(type, \": \", coalesce(outerMessage, innermostMessage)), severityLevel = toint(3), exProblemId = problemId, exMethod = method, exAssembly = assembly, exInnermostMessage = innermostMessage)",
        ["customEvents"] = "(customEvents | extend eventType = \"customEvent\", eventMessage = name)",
        ["availabilityResults"] = "(availabilityResults | extend eventType = \"availability\", eventMessage = strcat(name, \" \", success), severityLevel = toint(iff(success == \"True\", 1, 3)))",
        ["pageViews"] = "(pageViews | extend eventType = \"pageView\", eventMessage = strcat(name, \" \", duration, \"ms\"))",
    };

    private static readonly Dictionary<string, string> EventTypeToTable = new(StringComparer.OrdinalIgnoreCase)
    {
        [Models.EventType.Trace] = "traces",
        [Models.EventType.Request] = "requests",
        [Models.EventType.Dependency] = "dependencies",
        [Models.EventType.Exception] = "exceptions",
        [Models.EventType.CustomEvent] = "customEvents",
        [Models.EventType.Availability] = "availabilityResults",
        [Models.EventType.PageView] = "pageViews",
    };

    /// <summary>
    /// Wraps an alert's stored base query with its poll window and a count
    /// aggregation. The window is recomputed fresh each poll, so logs older than
    /// one check interval are never re-evaluated.
    /// </summary>
    // Filters on ingestion_time(), not timestamp: App Insights ingestion lags
    // minutes behind the event time, so a timestamp window that has already
    // moved past the event would silently miss it. The half-open interval
    // (from, to] matches the tiling poll windows — no double counting.
    public static string BuildCountKql(string baseQuery, DateTime from, DateTime to) =>
        $"{baseQuery}\n| where ingestion_time() > datetime({from:O}) and ingestion_time() <= datetime({to:O})\n| count";

    public static string BuildKql(
        string? freeText, TimeRangeRequest? timeRange, TagFilters tags, int limit,
        List<string>? eventTypes = null, int skip = 0)
    {
        var sb = new StringBuilder();

        // Determine which tables to include
        var tables = ResolveTableNames(eventTypes, tags.EventTypes);

        // Text filters applied INSIDE each table subquery, BEFORE the per-table
        // `top`, so matches outside the newest-N window aren't truncated away.
        // Free-text is AND'd with tag terms. Tag term groups are OR'd across tags
        // (each tag's terms are AND'd internally).
        var filterParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(freeText))
        {
            filterParts.Add($"\n   | where * contains \"{EscapeKql(freeText)}\"");
        }

        if (tags.TermGroups.Count > 0)
        {
            var groupClauses = tags.TermGroups.Select(group =>
            {
                var andParts = group.Select(t => $"* contains \"{EscapeKql(t)}\"");
                return $"({string.Join(" and ", andParts)})";
            });
            filterParts.Add($"\n   | where {string.Join(" or ", groupClauses)}");
        }

        var textFilter = string.Concat(filterParts);

        // Limit EACH table to `limit` rows *before* the union. Without this, a
        // single high-volume table (e.g. dependencies) consumes the entire global
        // limit and starves low-volume-but-important tables like exceptions — they
        // exist but never appear in the newest-N time-ordered window.
        // Each table must contribute the newest (skip + limit) rows, not just
        // `limit`: otherwise rows that fall into a later page (beyond the first
        // `limit` per table) would never reach the union and paging would repeat
        // the first page.
        var perTableTop = skip + limit;
        var projections = tables
            .Select(t => $"({TableProjections[t]}{textFilter}\n   | top {perTableTop} by timestamp desc)")
            .ToList();

        if (projections.Count == 1)
        {
            sb.Append(projections[0]);
        }
        else
        {
            sb.Append("union \n  ").Append(string.Join(",\n  ", projections));
        }

        sb.Append("\n| project timestamp, eventType, severityLevel = column_ifexists(\"severityLevel\", 0), eventMessage, customDimensions, ")
          .Append("duration = column_ifexists(\"duration\", 0.0), ")
          .Append("success = column_ifexists(\"success\", \"\"), ")
          .Append("resultCode = column_ifexists(\"resultCode\", \"\"), ")
          .Append("name = column_ifexists(\"name\", \"\"), ")
          .Append("target = column_ifexists(\"target\", \"\"), ")
          .Append("exProblemId = column_ifexists(\"exProblemId\", \"\"), ")
          .Append("exMethod = column_ifexists(\"exMethod\", \"\"), ")
          .Append("exAssembly = column_ifexists(\"exAssembly\", \"\"), ")
          .Append("exInnermostMessage = column_ifexists(\"exInnermostMessage\", \"\")");

        // Post-union clauses. Levels stay here because severityLevel is synthesized
        // per table (via column_ifexists in the project above) and only exists after
        // the union. Term/free-text filters are applied per-table above, before `top`.
        var clauses = new List<string>();

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
                clauses.Add($"severityLevel in ({string.Join(", ", levelInts)})");
            }
        }

        if (timeRange?.Type == "absolute" && timeRange.From.HasValue && timeRange.To.HasValue)
        {
            clauses.Add($"timestamp >= datetime({timeRange.From.Value:O})");
            clauses.Add($"timestamp <= datetime({timeRange.To.Value:O})");
        }

        if (clauses.Count > 0)
        {
            sb.Append("\n| where ").Append(string.Join("\n    and ", clauses));
        }

        sb.Append("\n| order by timestamp desc");

        // Page into the merged, time-ordered set. KQL has no OFFSET, so drop the
        // first `skip` rows via row_number() before taking the page.
        if (skip > 0)
        {
            sb.Append("\n| serialize _rn = row_number()");
            sb.Append($"\n| where _rn > {skip}");
            sb.Append("\n| project-away _rn");
        }

        sb.Append($"\n| limit {limit}");

        return sb.ToString();
    }

    private static List<string> ResolveTableNames(List<string>? eventTypes, List<string> tagEventTypes)
    {
        var requestedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (eventTypes is { Count: > 0 })
        {
            foreach (var et in eventTypes)
            {
                requestedTypes.Add(et);
            }
        }

        if (tagEventTypes.Count > 0)
        {
            foreach (var et in tagEventTypes)
            {
                requestedTypes.Add(et);
            }
        }

        if (requestedTypes.Count == 0)
        {
            return [.. TableProjections.Keys];
        }

        return requestedTypes
            .Where(et => EventTypeToTable.ContainsKey(et))
            .Select(et => EventTypeToTable[et])
            .Distinct()
            .ToList() is { Count: > 0 } resolved
                ? resolved
                : [.. TableProjections.Keys];
    }

    private static string? ToAppInsightsTimespan(TimeRangeRequest? timeRange)
    {
        if (timeRange?.Type != "relative")
        {
            return null;
        }

        return timeRange.Value switch
        {
            "15m" => "PT15M",
            "1h" => "PT1H",
            "6h" => "PT6H",
            "24h" => "P1D",
            "7d" => "P7D",
            _ => "PT1H",
        };
    }

    private static int LevelToSeverityInt(Models.LogLevel level) => level switch
    {
        Models.LogLevel.Trace => 0,
        Models.LogLevel.Info => 1,
        Models.LogLevel.Warn => 2,
        Models.LogLevel.Error => 3,
        _ => -1,
    };

    private static string EscapeKql(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ── Response mapping ─────────────────────────────────────────────────────

    private static List<LogEntry> MapToLogEntries(AppInsightsQueryResult result, string sourceName)
    {
        if (result.Tables is not [var table, ..])
        {
            return [];
        }

        var cols = table.Columns
            .Select((c, i) => (c.Name, Index: i))
            .ToDictionary(x => x.Name, x => x.Index);

        return table.Rows.Select(row =>
        {
            var dims = ParseCustomDimensions(row, cols);

            AddPropertyIfPresent(dims, row, cols, "duration", "duration");
            AddPropertyIfPresent(dims, row, cols, "success", "success");
            AddPropertyIfPresent(dims, row, cols, "resultCode", "resultCode");
            AddPropertyIfPresent(dims, row, cols, "name", "name");
            AddPropertyIfPresent(dims, row, cols, "target", "target");
            AddPropertyIfPresent(dims, row, cols, "exProblemId", "problemId");
            AddPropertyIfPresent(dims, row, cols, "exMethod", "method");
            AddPropertyIfPresent(dims, row, cols, "exAssembly", "assembly");
            AddPropertyIfPresent(dims, row, cols, "exInnermostMessage", "innermostMessage");

            return new LogEntry(
                Timestamp: ParseTimestamp(row, cols),
                Level: MapSeverity(TryGetInt(row, cols, "severityLevel")),
                Message: TryGetString(row, cols, "eventMessage") ?? "",
                Source: sourceName,
                EventType: TryGetString(row, cols, "eventType"),
                Properties: dims
            );
        }).ToList();
    }

    private static DateTimeOffset ParseTimestamp(JsonElement[] row, Dictionary<string, int> cols)
    {
        var raw = TryGetString(row, cols, "timestamp");
        return raw is not null && DateTimeOffset.TryParse(raw, out var ts) ? ts : DateTimeOffset.UtcNow;
    }

    private static void AddPropertyIfPresent(
        Dictionary<string, string> dims, JsonElement[] row, Dictionary<string, int> cols,
        string columnName, string propertyName)
    {
        var value = TryGetString(row, cols, columnName);
        if (!string.IsNullOrEmpty(value))
        {
            dims[propertyName] = value;
        }
    }

    private static Dictionary<string, string> ParseCustomDimensions(JsonElement[] row, Dictionary<string, int> cols)
    {
        if (!cols.TryGetValue("customDimensions", out var idx) || idx >= row.Length)
        {
            return [];
        }

        var el = row[idx];
        try
        {
            var json = el.ValueKind switch
            {
                JsonValueKind.Object => el.GetRawText(),
                JsonValueKind.String => el.GetString(),
                _ => null,
            };
            if (string.IsNullOrEmpty(json))
            {
                return [];
            }

            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (parsed is null)
            {
                return [];
            }

            return parsed.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ValueKind == JsonValueKind.String
                    ? kv.Value.GetString() ?? ""
                    : kv.Value.GetRawText());
        }
        catch
        {
            return [];
        }
    }

    private static string? TryGetString(JsonElement[] row, Dictionary<string, int> cols, string name)
    {
        if (!cols.TryGetValue(name, out var idx) || idx >= row.Length)
        {
            return null;
        }

        var el = row[idx];
        return el.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => el.GetString(),
            _ => el.GetRawText(),
        };
    }

    private static int? TryGetInt(JsonElement[] row, Dictionary<string, int> cols, string name)
    {
        if (!cols.TryGetValue(name, out var idx) || idx >= row.Length)
        {
            return null;
        }

        var el = row[idx];
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
        {
            return n;
        }

        return int.TryParse(TryGetString(row, cols, name), out var v) ? v : null;
    }

    private static Models.LogLevel MapSeverity(int? level) => level switch
    {
        0 => Models.LogLevel.Trace,
        1 => Models.LogLevel.Info,
        2 => Models.LogLevel.Warn,
        3 or 4 => Models.LogLevel.Error,
        _ => Models.LogLevel.Info,
    };
}

public sealed class AppInsightsQueryException(int statusCode, string body)
    : Exception($"App Insights returned {statusCode}: {body}")
{
    public int StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}

// ── Response DTOs ──────────────────────────────────────────────────────────────

public record AppInsightsQueryResult(List<AppInsightsTable> Tables);

public record AppInsightsTable(
    string Name,
    List<AppInsightsColumn> Columns,
    List<JsonElement[]> Rows);

public record AppInsightsColumn(string Name, string Type);
