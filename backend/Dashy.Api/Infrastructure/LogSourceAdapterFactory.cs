using Dashy.Api.Data.Entities;

namespace Dashy.Api.Infrastructure;

public sealed class LogSourceAdapterFactory(
    AppInsightsAdapter appInsights,
    LokiAdapter loki) : ILogSourceAdapterFactory
{
    public ILogSourceAdapter GetAdapter(SourceType type) => type switch
    {
        SourceType.AppInsights => appInsights,
        SourceType.Loki => loki,
        _ => throw new InvalidOperationException($"No adapter registered for source type: {type}"),
    };
}
