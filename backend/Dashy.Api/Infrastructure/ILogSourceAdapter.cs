using Dashy.Api.Data.Entities;
using Dashy.Api.Models;
using Dashy.Api.Services;

namespace Dashy.Api.Infrastructure;

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
