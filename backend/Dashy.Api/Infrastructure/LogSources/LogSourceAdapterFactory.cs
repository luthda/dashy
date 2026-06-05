using Dashy.Api.Application.Abstractions;
using Dashy.Api.Domain.Entities;

namespace Dashy.Api.Infrastructure.LogSources;

public sealed class LogSourceAdapterFactory(
    AppInsightsAdapter appInsights) : ILogSourceAdapterFactory
{
    public ILogSourceAdapter GetAdapter(SourceType type) => type switch
    {
        SourceType.AppInsights => appInsights,
        _ => throw new InvalidOperationException($"No adapter registered for source type: {type}"),
    };
}
