namespace Dashy.Api.Domain.Models;

public record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Message,
    string Source,
    string? EventType,
    Dictionary<string, string> Properties
);
