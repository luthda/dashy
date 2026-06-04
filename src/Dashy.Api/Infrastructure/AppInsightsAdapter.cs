using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dashy.Api.Data.Entities;
using Dashy.Api.Models;
using Dashy.Api.Services;

namespace Dashy.Api.Infrastructure;

public sealed class AppInsightsAdapter(HttpClient http, ILogger<AppInsightsAdapter> logger) : ILogSourceAdapter
{
    private const string BaseUrl = "https://api.applicationinsights.io/v1/apps";

    public SourceType SourceType => SourceType.AppInsights;

    public async Task<List<LogEntry>> QueryAsync(AdapterQueryRequest request, CancellationToken ct)
    {
        var cfg = JsonSerializer.Deserialize<AppInsightsConfig>(request.ConfigJson, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Invalid App Insights config");

        var kql = BuildKql(request.FreeText, request.TimeRange, request.TagFilters, request.Limit);
        var timespan = ToAppInsightsTimespan(request.TimeRange);

        logger.LogDebug("App Insights KQL: {Kql}", kql);

        return await ExecuteQueryAsync(cfg.AppId, cfg.ApiKey, kql, timespan, request.SourceName, ct);
    }

    public async Task TestConnectionAsync(string configJson, string sourceName, CancellationToken ct)
    {
        var cfg = JsonSerializer.Deserialize<AppInsightsConfig>(configJson, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Invalid App Insights config");

        await ExecuteQueryAsync(cfg.AppId, cfg.ApiKey, "traces | limit 1", null, sourceName, ct);
    }

    private async Task<List<LogEntry>> ExecuteQueryAsync(
        string appId, string apiKey, string kql, string? timespan,
        string sourceName, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{appId}/query");
        request.Headers.Add("X-Api-Key", apiKey);
        request.Content = JsonContent.Create(new { query = kql, timespan });

        using var response = await http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("App Insights query failed {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new AppInsightsQueryException((int)response.StatusCode, body);
        }

        var result = await response.Content
            .ReadFromJsonAsync<AppInsightsQueryResult>(ct)
            ?? throw new InvalidOperationException("Empty response from App Insights");

        return MapToLogEntries(result, sourceName);
    }

    // ── KQL builder ──────────────────────────────────────────────────────────

    public static string BuildKql(
        string? freeText, TimeRangeRequest? timeRange, TagFilters tags, int limit)
    {
        var sb = new StringBuilder("traces");
        var clauses = new List<string>();

        if (!string.IsNullOrWhiteSpace(freeText))
            clauses.Add($"message contains \"{EscapeKql(freeText)}\"");

        if (tags.Levels.Count > 0)
        {
            var levelInts = tags.Levels
                .Select(LevelToSeverityInt)
                .Where(v => v >= 0)
                .Distinct()
                .OrderBy(v => v)
                .ToList();

            if (levelInts.Count > 0)
                clauses.Add($"severityLevel in ({string.Join(", ", levelInts)})");
        }

        foreach (var et in tags.EventTypes)
            clauses.Add($"customDimensions[\"EventType\"] == \"{EscapeKql(et)}\"");

        foreach (var term in tags.Terms)
            clauses.Add($"message contains \"{EscapeKql(term)}\"");

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
            return [];

        var cols = table.Columns
            .Select((c, i) => (c.Name, Index: i))
            .ToDictionary(x => x.Name, x => x.Index);

        return table.Rows.Select(row =>
        {
            var dims = ParseCustomDimensions(row, cols);
            return new LogEntry(
                Timestamp: ParseTimestamp(row, cols),
                Level: MapSeverity(TryGetInt(row, cols, "severityLevel")),
                Message: TryGetString(row, cols, "message")
                         ?? TryGetString(row, cols, "outerMessage")
                         ?? "",
                Source: sourceName,
                EventType: dims.GetValueOrDefault("EventType"),
                Properties: dims
            );
        }).ToList();
    }

    private static DateTimeOffset ParseTimestamp(JsonElement[] row, Dictionary<string, int> cols)
    {
        var raw = TryGetString(row, cols, "timestamp");
        return raw is not null && DateTimeOffset.TryParse(raw, out var ts) ? ts : DateTimeOffset.UtcNow;
    }

    private static Dictionary<string, string> ParseCustomDimensions(JsonElement[] row, Dictionary<string, int> cols)
    {
        if (!cols.TryGetValue("customDimensions", out var idx) || idx >= row.Length) return [];

        var el = row[idx];
        try
        {
            // App Insights may return customDimensions as a JSON object or as a
            // JSON-encoded string. Handle both, and tolerate non-string values.
            var json = el.ValueKind switch
            {
                JsonValueKind.Object => el.GetRawText(),
                JsonValueKind.String => el.GetString(),
                _ => null,
            };
            if (string.IsNullOrEmpty(json)) return [];

            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (parsed is null) return [];

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
        if (!cols.TryGetValue(name, out var idx) || idx >= row.Length) return null;
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
        if (!cols.TryGetValue(name, out var idx) || idx >= row.Length) return null;
        var el = row[idx];
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
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
