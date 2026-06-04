# Azure Application Insights — REST Query API Reference

_Used by the Dashy .NET proxy to query logs, traces, and exceptions from App Insights._

---

## Authentication: API key method

Dashy uses **API key + Application ID** — not a connection string, not OAuth.

| Value | Where to find it | Stored in Dashy as |
|---|---|---|
| **Application ID** | Azure Portal → App Insights → Configure → API Access | `sources.config.appId` |
| **API Key** | Same page → Create API key (Read telemetry only) | `sources.config.apiKey` (encrypted) |

> The connection string (`InstrumentationKey=...`) is for **sending** telemetry only — it cannot query logs. If a user pastes a connection string, tell them to generate an API key instead.

Every query request sends:
```
X-Api-Key: <apiKey>
```
No Bearer token or OAuth flow required.

---

## Query endpoint

```
POST https://api.applicationinsights.io/v1/apps/{appId}/query
```

### Request body
```json
{
  "query": "traces | order by timestamp desc | limit 500",
  "timespan": "PT1H"
}
```

| Field | Required | Notes |
|---|---|---|
| `query` | ✅ | KQL (Kusto Query Language) string |
| `timespan` | ❌ | ISO 8601 duration — `PT15M`, `PT1H`, `PT6H`, `P1D`, `P7D`. Applied on top of any `where timestamp` in the query |
| `applications` | ❌ | Array of additional Application IDs for cross-app queries |

### Response shape
```json
{
  "tables": [{
    "name": "PrimaryResult",
    "columns": [
      { "name": "timestamp",     "type": "datetime" },
      { "name": "message",       "type": "string"   },
      { "name": "severityLevel", "type": "int"      },
      { "name": "customDimensions", "type": "dynamic" }
    ],
    "rows": [
      ["2026-06-04T10:00:00Z", "Application started", 1, "{\"EventType\":\"Startup\"}"],
      ["2026-06-04T10:01:00Z", "Exception in handler", 3, "{\"EventType\":\"Exception\"}"]
    ]
  }]
}
```

Rows are **positional** — map by column index, not by name.

---

## Key KQL tables

| Table | Contains | Use for |
|---|---|---|
| `traces` | Log messages (ILogger, TrackTrace) | Main log view |
| `exceptions` | Caught & uncaught exceptions | Error investigation |
| `requests` | Inbound HTTP request telemetry | Performance / errors |
| `customEvents` | Custom TrackEvent calls | Business events |
| `dependencies` | Outbound calls (DB, HTTP, queues) | Dependency tracing |

---

## Severity level mapping

| `severityLevel` int | Maps to `LogEntry.Level` |
|---|---|
| 0 | `trace` |
| 1 | `info` |
| 2 | `warn` |
| 3 | `error` |
| 4 | `error` (Critical) |

```csharp
private static string MapSeverity(int? level) => level switch
{
    0 => "trace",
    1 => "info",
    2 => "warn",
    3 => "error",
    4 => "error",
    _ => "info"
};
```

---

## Mapping to LogEntry

```csharp
private static List<LogEntry> MapToLogEntries(AppInsightsQueryResult result, string sourceName)
{
    var table = result.Tables[0];
    var cols = table.Columns
        .Select((c, i) => (c.Name, Index: i))
        .ToDictionary(x => x.Name, x => x.Index);

    return table.Rows.Select(row =>
    {
        var dims = ParseCustomDimensions(row, cols);
        return new LogEntry(
            Timestamp:  DateTimeOffset.Parse(row[cols["timestamp"]]),
            Level:      MapSeverity(row.TryGetInt(cols, "severityLevel")),
            Message:    row.TryGetString(cols, "message") ?? row.TryGetString(cols, "outerMessage") ?? "",
            Source:     sourceName,
            EventType:  dims.GetValueOrDefault("EventType"),
            Properties: dims
        );
    }).ToList();
}

private static Dictionary<string, string> ParseCustomDimensions(string[] row, Dictionary<string, int> cols)
{
    if (!cols.TryGetValue("customDimensions", out var idx)) return [];
    var raw = row[idx];
    if (string.IsNullOrEmpty(raw)) return [];
    return JsonSerializer.Deserialize<Dictionary<string, string>>(raw) ?? [];
}
```

---

## Example KQL queries

```kql
// All traces, newest first
traces | order by timestamp desc | limit 500

// Free text search
traces | where message contains "winfap" | order by timestamp desc | limit 500

// Filter by EventType (in customDimensions)
traces | where customDimensions["EventType"] == "Exception" | order by timestamp desc | limit 500

// Filter by level (Error and Critical only)
traces | where severityLevel >= 3 | order by timestamp desc | limit 500

// Combined tag filter (AND)
traces
| where message contains "winfap"
    and customDimensions["EventType"] == "Exception"
    and severityLevel >= 3
| order by timestamp desc
| limit 500

// Bar chart: log counts bucketed by time, coloured by level
traces
| summarize count() by bin(timestamp, 5m), severityLevel
| order by timestamp asc
```

---

## .NET client skeleton

```csharp
// Infrastructure/AppInsightsClient.cs
public sealed class AppInsightsClient(HttpClient http)
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
        request.Content = JsonContent.Create(new
        {
            query    = kql,
            timespan = timespan   // null = rely on the query's own where clause
        });

        using var response = await http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new AppInsightsQueryException((int)response.StatusCode, body);
        }

        var result = await response.Content
            .ReadFromJsonAsync<AppInsightsQueryResult>(ct)
            ?? throw new InvalidOperationException("Empty response from App Insights");

        return MapToLogEntries(result, sourceName);
    }
}

// Register in Program.cs
builder.Services.AddHttpClient<AppInsightsClient>();
```

---

## Error handling

| HTTP status | Meaning | Action |
|---|---|---|
| `400` | Invalid KQL syntax | Surface the error body as an inline query error in the UI |
| `403` | Wrong Application ID, or API key lacks `ReadTelemetry` | Mark source as `error`, prompt user to re-check credentials |
| `404` | Application ID not found | Same as 403 handling |
| `429` | Rate limited | Retry with exponential backoff (cap at 3 retries); log a warning |
| `200` with empty `rows` | Query matched nothing | Return empty list — UI shows empty state |
