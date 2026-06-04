using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Dashy.Api.Models;

namespace Dashy.Api.Infrastructure;

public sealed class LokiClient(HttpClient http, ILogger<LokiClient> logger)
{
    /// <param name="baseUrl">e.g. http://loki:3100</param>
    /// <param name="orgId">Optional Loki tenant ID (X-Scope-OrgID header)</param>
    /// <param name="authToken">Optional Bearer token</param>
    /// <param name="logql">LogQL stream selector + pipeline, e.g. {app="api"} |= "error"</param>
    /// <param name="start">Unix nanoseconds (as string)</param>
    /// <param name="end">Unix nanoseconds (as string)</param>
    /// <param name="limit">Max lines to return</param>
    public async Task<List<LogEntry>> QueryAsync(
        string baseUrl,
        string? orgId,
        string? authToken,
        string logql,
        string start,
        string end,
        int limit,
        string sourceName,
        CancellationToken ct)
    {
        var qs = HttpUtility.ParseQueryString("");
        qs["query"] = logql;
        qs["start"] = start;
        qs["end"]   = end;
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

    private static List<LogEntry> MapToLogEntries(LokiQueryResponse response, string sourceName)
    {
        var entries = new List<LogEntry>();

        foreach (var stream in response.Data.Result)
        {
            var labels = stream.Stream ?? [];

            // Pick the level from the stream labels; fall back to "info"
            var level = NormaliseLevel(
                labels.GetValueOrDefault("level")
             ?? labels.GetValueOrDefault("severity")
             ?? labels.GetValueOrDefault("log_level"));

            foreach (var value in stream.Values)
            {
                var tsNanos = value[0];
                var line    = value[1];

                var timestamp = ParseLokiTimestamp(tsNanos);

                // Try to parse structured JSON line for richer properties
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
                    Timestamp:  timestamp,
                    Level:      level,
                    Message:    line,
                    Source:     sourceName,
                    EventType:  eventType,
                    Properties: properties
                ));
            }
        }

        // Newest first
        entries.Sort((a, b) => DateTimeOffset.Compare(b.Timestamp, a.Timestamp));
        return entries;
    }

    private static DateTimeOffset ParseLokiTimestamp(string nanoseconds)
    {
        if (long.TryParse(nanoseconds, out var ns))
        {
            var ticks = ns / 100; // nanoseconds → ticks (100ns)
            return new DateTimeOffset(
                DateTimeOffset.UnixEpoch.Ticks + ticks,
                TimeSpan.Zero);
        }
        return DateTimeOffset.UtcNow;
    }

    private static string NormaliseLevel(string? raw) => raw?.ToLowerInvariant() switch
    {
        "error" or "err" or "fatal" or "critical" => "error",
        "warn"  or "warning"                       => "warn",
        "info"  or "information"                   => "info",
        "debug"                                    => "debug",
        "trace"                                    => "trace",
        _                                          => "info",
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
    List<string[]> Values);  // [timestamp_ns, line]
