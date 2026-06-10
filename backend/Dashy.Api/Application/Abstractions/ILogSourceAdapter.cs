using Dashy.Api.Application.Services;
using Dashy.Api.Domain.Entities;
using Dashy.Api.Domain.Models;

namespace Dashy.Api.Application.Abstractions;

public interface ILogSourceAdapter
{
    SourceType SourceType { get; }
    Task<List<LogEntry>> QueryAsync(AdapterQueryRequest request, CancellationToken ct);
    Task TestConnectionAsync(string configJson, string sourceName, CancellationToken ct);

    /// <summary>
    /// Counts entries matching the base query within the [From, To] window.
    /// Used by alert polling — the window is injected at execution time so the
    /// stored query stays reusable across polls.
    /// </summary>
    Task<int> CountAsync(AdapterCountRequest request, CancellationToken ct);
}

public record AdapterCountRequest(
    string ConfigJson,
    string SourceName,
    string BaseQuery,
    DateTime From,
    DateTime To);

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
