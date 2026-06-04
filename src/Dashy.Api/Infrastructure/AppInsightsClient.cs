using System.Net.Http.Json;
using System.Text.Json;
using Dashy.Api.Models;

namespace Dashy.Api.Infrastructure;

public sealed class AppInsightsClient(HttpClient http, ILogger<AppInsightsClient> logger)
{
    private const string BaseUrl = "https://api.applicationinsights.io/v1/apps";

    public async Task<List<LogEntry>> QueryAsync(
        string appId,
        string apiKey,
        string kql,
        string? timespan,
        string sourceName,
        CancellationToken ct)
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
                Timestamp:  ParseTimestamp(row, cols),
                Level:      MapSeverity(TryGetInt(row, cols, "severityLevel")),
                Message:    TryGetString(row, cols, "message")
                         ?? TryGetString(row, cols, "outerMessage")
                         ?? "",
                Source:     sourceName,
                EventType:  dims.GetValueOrDefault("EventType"),
                Properties: dims
            );
        }).ToList();
    }

    private static DateTimeOffset ParseTimestamp(string?[] row, Dictionary<string, int> cols)
    {
        var raw = TryGetString(row, cols, "timestamp");
        return raw is not null && DateTimeOffset.TryParse(raw, out var ts) ? ts : DateTimeOffset.UtcNow;
    }

    private static Dictionary<string, string> ParseCustomDimensions(string?[] row, Dictionary<string, int> cols)
    {
        var raw = TryGetString(row, cols, "customDimensions");
        if (string.IsNullOrEmpty(raw)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(raw) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string? TryGetString(string?[] row, Dictionary<string, int> cols, string name)
    {
        if (!cols.TryGetValue(name, out var idx) || idx >= row.Length) return null;
        return row[idx];
    }

    private static int? TryGetInt(string?[] row, Dictionary<string, int> cols, string name)
    {
        var s = TryGetString(row, cols, name);
        return int.TryParse(s, out var v) ? v : null;
    }

    private static string MapSeverity(int? level) => level switch
    {
        0 => "trace",
        1 => "info",
        2 => "warn",
        3 => "error",
        4 => "error",
        _ => "info",
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
    List<string?[]> Rows);

public record AppInsightsColumn(string Name, string Type);
