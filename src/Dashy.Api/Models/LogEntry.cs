namespace Dashy.Api.Models;

/// <summary>
/// Normalised log entry — produced by both AppInsightsClient and LokiClient.
/// </summary>
public record LogEntry(
    DateTimeOffset Timestamp,
    string Level,               // "error" | "warn" | "info" | "debug" | "trace"
    string Message,
    string Source,              // display name of the source
    string? EventType,          // App Insights customDimensions.EventType or Loki label
    Dictionary<string, string> Properties   // remaining fields
);
