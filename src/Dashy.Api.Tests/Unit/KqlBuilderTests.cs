using Dashy.Api.Infrastructure;
using Dashy.Api.Models;
using Dashy.Api.Services;
using FluentAssertions;

namespace Dashy.Api.Tests.Unit;

public class KqlBuilderTests
{
    [Fact]
    public void BuildKql_NoFilters_UnionsAllTables()
    {
        var kql = AppInsightsAdapter.BuildKql(null, null, TagFilters.Empty, 100);

        kql.Should().Contain("union");
        kql.Should().Contain("traces");
        kql.Should().Contain("requests");
        kql.Should().Contain("dependencies");
        kql.Should().Contain("exceptions");
        kql.Should().Contain("customEvents");
        kql.Should().Contain("availabilityResults");
        kql.Should().Contain("pageViews");
    }

    [Fact]
    public void BuildKql_FreeTextOnly_ContainsContainsClause()
    {
        var kql = AppInsightsAdapter.BuildKql("winfap", null, TagFilters.Empty, 100);
        kql.Should().Contain("eventMessage contains \"winfap\"");
    }

    [Fact]
    public void BuildKql_EventTypeFilter_RestrictsToMatchingTable()
    {
        var kql = AppInsightsAdapter.BuildKql(null, null, TagFilters.Empty, 100,
            eventTypes: [EventType.Exception]);

        kql.Should().Contain("exceptions");
        kql.Should().NotContain("union");
        kql.Should().NotContain("(traces");
    }

    [Fact]
    public void BuildKql_MultipleEventTypes_UnionsOnlyMatchingTables()
    {
        var kql = AppInsightsAdapter.BuildKql(null, null, TagFilters.Empty, 100,
            eventTypes: [EventType.Request, EventType.Dependency]);

        kql.Should().Contain("union");
        kql.Should().Contain("requests");
        kql.Should().Contain("dependencies");
        kql.Should().NotContain("(traces");
        kql.Should().NotContain("exceptions");
    }

    [Fact]
    public void BuildKql_TagEventTypes_RestrictsToMatchingTables()
    {
        var tags = new TagFilters([], [], [EventType.Trace]);
        var kql = AppInsightsAdapter.BuildKql(null, null, tags, 100);

        kql.Should().Contain("traces");
        kql.Should().NotContain("union");
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
        var tags = new TagFilters([], [LogLevel.Error], []);
        var kql = AppInsightsAdapter.BuildKql("winfap", null, tags, 500,
            eventTypes: [EventType.Exception]);

        kql.Should().Contain("eventMessage contains \"winfap\"");
        kql.Should().Contain("severityLevel in (3)");
        kql.Should().Contain("exceptions");
        kql.Should().Contain("limit 500");
    }

    [Fact]
    public void BuildKql_ProjectsCommonColumns()
    {
        var kql = AppInsightsAdapter.BuildKql(null, null, TagFilters.Empty, 100);

        kql.Should().Contain("| project timestamp, eventType, severityLevel = column_ifexists(\"severityLevel\", 0), eventMessage, customDimensions");
        kql.Should().Contain("column_ifexists(\"duration\"");
        kql.Should().Contain("column_ifexists(\"success\"");
        kql.Should().Contain("column_ifexists(\"resultCode\"");
        kql.Should().Contain("column_ifexists(\"name\"");
        kql.Should().Contain("column_ifexists(\"target\"");
        kql.Should().Contain("column_ifexists(\"exProblemId\"");
        kql.Should().Contain("column_ifexists(\"exMethod\"");
    }

    [Fact]
    public void BuildKql_AbsoluteTimeRange_AddsTimestampClauses()
    {
        var timeRange = new TimeRangeRequest("absolute", null,
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        var kql = AppInsightsAdapter.BuildKql(null, timeRange, TagFilters.Empty, 100);

        kql.Should().Contain("timestamp >= datetime(");
        kql.Should().Contain("timestamp <= datetime(");
    }

    [Fact]
    public void BuildKql_ExceptionTable_ExtendsSeverityAndExceptionFields()
    {
        var kql = AppInsightsAdapter.BuildKql(null, null, TagFilters.Empty, 100,
            eventTypes: [EventType.Exception]);

        kql.Should().Contain("severityLevel = 3");
        kql.Should().Contain("coalesce(outerMessage, innermostMessage)");
        kql.Should().Contain("exProblemId = problemId");
        kql.Should().Contain("exMethod = method");
    }

    [Fact]
    public void BuildKql_TermFilter_ContainsContainsClause()
    {
        var tags = new TagFilters(["error-code-42"], [], []);
        var kql = AppInsightsAdapter.BuildKql(null, null, tags, 100);

        kql.Should().Contain("eventMessage contains \"error-code-42\"");
    }
}
