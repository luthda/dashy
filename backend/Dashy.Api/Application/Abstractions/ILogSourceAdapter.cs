using Dashy.Api.Application.Services;
using Dashy.Api.Domain.Entities;
using Dashy.Api.Domain.Models;

namespace Dashy.Api.Application.Abstractions;

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
