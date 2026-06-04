using Dashy.Api.Data.Entities;
using Dashy.Api.Models;

namespace Dashy.Api.Services;

public interface ILogSourceAdapter
{
    SourceType SourceType { get; }
    Task<List<LogEntry>> QueryAsync(AdapterQueryRequest request, CancellationToken ct);
    Task TestConnectionAsync(string configJson, string sourceName, CancellationToken ct);
}

public record AdapterQueryRequest(
    string ConfigJson,
    string SourceName,
    string? FreeText,
    TimeRangeRequest? TimeRange,
    TagFilters TagFilters,
    int Limit,
    List<string>? EventTypes = null,
    int? Skip = null);

public interface ILogSourceAdapterFactory
{
    ILogSourceAdapter GetAdapter(SourceType type);
}
