using Dashy.Api.Infrastructure;
using Dashy.Api.Models;
using Dashy.Api.Services;
using FluentAssertions;

namespace Dashy.Api.Tests.Unit;

public class KqlBuilderTests
{
    [Fact]
    public void BuildKql_FreeTextOnly_ContainsContainsClause()
    {
        var kql = AppInsightsAdapter.BuildKql("winfap", null, TagFilters.Empty, 100);
        kql.Should().Contain("message contains \"winfap\"");
    }

    [Fact]
    public void BuildKql_EventTypeFilter_AddedToWhere()
    {
        var tags = new TagFilters([], [], ["Exception"]);
        var kql = AppInsightsAdapter.BuildKql(null, null, tags, 100);
        kql.Should().Contain("customDimensions[\"EventType\"] == \"Exception\"");
    }

    [Fact]
    public void BuildKql_LevelFilter_MapsToSeverityLevel()
    {
        var tags = new TagFilters([], [LogLevel.Error], []);
        var kql = AppInsightsAdapter.BuildKql(null, null, tags, 100);
        kql.Should().Contain("severityLevel in (3)");
    }

    [Fact]
    public void BuildKql_Combined_AllClausesPresent()
    {
        var tags = new TagFilters([], [LogLevel.Error], ["Exception"]);
        var kql = AppInsightsAdapter.BuildKql("winfap", null, tags, 500);
        kql.Should().Contain("message contains \"winfap\"");
        kql.Should().Contain("severityLevel in (3)");
        kql.Should().Contain("customDimensions[\"EventType\"] == \"Exception\"");
        kql.Should().Contain("limit 500");
    }
}
