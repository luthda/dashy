using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;
using Dashy.Api.Data.Entities;
using Dashy.Api.Models;
using Dashy.Api.Services;

namespace Dashy.Api.Infrastructure;

public sealed class LokiAdapter(HttpClient http, ILogger<LokiAdapter> logger) : ILogSourceAdapter
{
    public SourceType SourceType => SourceType.Loki;

    public async Task<List<LogEntry>> QueryAsync(AdapterQueryRequest request, CancellationToken ct)
    {
        var cfg = JsonSerializer.Deserialize<LokiConfig>(request.ConfigJson)
            ?? throw new InvalidOperationException("Invalid Loki config");

        var (start, end) = ToLokiTimeRange(request.TimeRange);
        var logql = BuildLogQL(request.FreeText, request.TagFilters);

        logger.LogDebug("Loki LogQL: {LogQL}", logql);

        return await ExecuteQueryAsync(
            cfg.BaseUrl, cfg.OrgId, cfg.AuthToken,
            logql, start, end, request.Limit, request.SourceName, ct);
    }

    public async Task TestConnectionAsync(string configJson, string sourceName, CancellationToken ct)
    {
        var cfg = JsonSerializer.Deserialize<LokiConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid Loki config");

        var now = DateTimeOffset.UtcNow;
        var end = (now.ToUnixTimeSeconds() * 1_000_000_000L).ToString();
        var start = (now.AddMinutes(-1).ToUnixTimeSeconds() * 1_000_000_000L).ToString();

        await ExecuteQueryAsync(
            cfg.BaseUrl, cfg.OrgId, cfg.AuthToken,
            "{app=\"__test__\"}", start, end, 1, sourceName, ct);
    }

    private async Task<List<LogEntry>> ExecuteQueryAsync(
        string baseUrl, string? orgId, string? authToken,
        string logql, string start, string end, int limit,
        string sourceName, CancellationToken ct)
    {
        var qs = HttpUtility.ParseQueryString("");
        qs["query"] = logql;
        qs["start"] = start;
        qs["end"] = end;
        qs["limit"] = limit.ToString();
        qs["direction"] = "BACKWARD";

        var uri = $"{baseUrl.TrimEnd('/')}/loki/api/v1/query_range?{qs}";

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);

        if (!string.IsNullOrEmpty(authToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

        if (!string.IsNullOrEmpty(orgId))
            request.Headers.Add("X-Scope-OrgID", orgId);

        using var response = await http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Loki query failed {StatusCode}: {Body}",
                (int)response.StatusCode, body);
            throw new LokiQueryException((int)response.StatusCode, body);
        }

        var result = await response.Content
            .ReadFromJsonAsync<LokiQueryResponse>(ct)
            ?? throw new InvalidOperationException("Empty response from Loki");

        return MapToLogEntries(result, sourceName);
    }

    // ── LogQL builder ────────────────────────────────────────────────────────

    public static string BuildLogQL(string? freeText, TagFilters tags)
    {
        var sb = new StringBuilder("{job=~\".+\"}");

        if (!string.IsNullOrWhiteSpace(freeText))
            sb.Append($" |= \"{EscapeLogQL(freeText)}\"");

        foreach (var term in tags.Terms)
            sb.Append($" |= \"{EscapeLogQL(term)}\"");

        foreach (var level in tags.Levels)
            sb.Append($" | level=\"{level.ToString().ToLowerInvariant()}\"");

        foreach (var et in tags.EventTypes)
            sb.Append($" | json | EventType=\"{EscapeLogQL(et)}\"");

        return sb.ToString();
    }

    private static (string start, string end) ToLokiTimeRange(TimeRangeRequest? timeRange)
    {
        var now = DateTimeOffset.UtcNow;
        var to = now;
        var from = timeRange?.Type switch
        {
            "relative" => timeRange.Value switch
            {
                "15m" => now.AddMinutes(-15),
                "6h" => now.AddHours(-6),
                "24h" => now.AddHours(-24),
                "7d" => now.AddDays(-7),
                _ => now.AddHours(-1),
            },
            "absolute" when timeRange.From.HasValue && timeRange.To.HasValue
                => new DateTimeOffset(timeRange.From.Value, TimeSpan.Zero),
            _ => now.AddHours(-1),
        };

        if (timeRange?.Type == "absolute" && timeRange.To.HasValue)
            to = new DateTimeOffset(timeRange.To.Value, TimeSpan.Zero);

        return (
            (from.ToUnixTimeSeconds() * 1_000_000_000L).ToString(),
            (to.ToUnixTimeSeconds() * 1_000_000_000L).ToString()
        );
    }

    private static string EscapeLogQL(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ── Response mapping ─────────────────────────────────────────────────────

    private static List<LogEntry> MapToLogEntries(LokiQueryResponse response, string sourceName)
    {
        var entries = new List<LogEntry>();

        foreach (var stream in response.Data.Result)
        {
            var labels = stream.Stream ?? [];

            var level = NormaliseLevel(
                labels.GetValueOrDefault("level")
                ?? labels.GetValueOrDefault("severity")
                ?? labels.GetValueOrDefault("log_level"));

            foreach (var value in stream.Values)
            {
                var tsNanos = value[0];
                var line = value[1];

                var timestamp = ParseLokiTimestamp(tsNanos);

                var properties = new Dictionary<string, string>(labels, StringComparer.OrdinalIgnoreCase);
                string? eventType = null;

                if (line.TrimStart().StartsWith('{'))
                {
                    try
                    {
                        var doc = JsonDocument.Parse(line);
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (prop.Name.Equals("level", StringComparison.OrdinalIgnoreCase)
                                || prop.Name.Equals("severity", StringComparison.OrdinalIgnoreCase))
                            {
                                level = NormaliseLevel(prop.Value.GetString());
                            }
                            else if (prop.Name.Equals("EventType", StringComparison.OrdinalIgnoreCase))
                            {
                                eventType = prop.Value.GetString();
                            }
                            else
                            {
                                properties[prop.Name] = prop.Value.ToString();
                            }
                        }
                    }
                    catch { /* non-JSON line — keep raw */ }
                }

                entries.Add(new LogEntry(
                    Timestamp: timestamp,
                    Level: level,
                    Message: line,
                    Source: sourceName,
                    EventType: eventType,
                    Properties: properties
                ));
            }
        }

        entries.Sort((a, b) => DateTimeOffset.Compare(b.Timestamp, a.Timestamp));
        return entries;
    }

    private static DateTimeOffset ParseLokiTimestamp(string nanoseconds)
    {
        if (long.TryParse(nanoseconds, out var ns))
        {
            var ticks = ns / 100;
            return new DateTimeOffset(
                DateTimeOffset.UnixEpoch.Ticks + ticks,
                TimeSpan.Zero);
        }
        return DateTimeOffset.UtcNow;
    }

    private static Models.LogLevel NormaliseLevel(string? raw) => raw?.ToLowerInvariant() switch
    {
        "error" or "err" or "fatal" or "critical" => Models.LogLevel.Error,
        "warn" or "warning" => Models.LogLevel.Warn,
        "info" or "information" => Models.LogLevel.Info,
        "debug" => Models.LogLevel.Debug,
        "trace" => Models.LogLevel.Trace,
        _ => Models.LogLevel.Info,
    };
}

public sealed class LokiQueryException(int statusCode, string body)
    : Exception($"Loki returned {statusCode}: {body}")
{
    public int StatusCode { get; } = statusCode;
    public string Body { get; } = body;
}

// ── Response DTOs ──────────────────────────────────────────────────────────────

public record LokiQueryResponse(LokiQueryData Data);

public record LokiQueryData(
    string ResultType,
    List<LokiStream> Result);

public record LokiStream(
    Dictionary<string, string>? Stream,
    List<string[]> Values);
