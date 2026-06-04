using Dashy.Api.Data.Entities;
using Dashy.Api.Services;

namespace Dashy.Api.Infrastructure;

public sealed class LogSourceAdapterFactory(
    AppInsightsAdapter appInsights) : ILogSourceAdapterFactory
{
    public ILogSourceAdapter GetAdapter(SourceType type) => type switch
    {
        SourceType.AppInsights => appInsights,
        _ => throw new InvalidOperationException($"No adapter registered for source type: {type}"),
    };
}
