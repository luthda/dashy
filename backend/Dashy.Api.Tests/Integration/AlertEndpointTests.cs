using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dashy.Api.Controllers;
using Dashy.Api.Domain.Entities;
using Dashy.Api.Infrastructure.Persistence;
using Dashy.Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Dashy.Api.Tests.Integration;

public class AlertEndpointTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly DashyWebApplicationFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _sourceId;

    public async Task InitializeAsync()
    {
        await _factory.InitialiseDatabaseAsync();
        _client = _factory.CreateClient();

        var response = await _client.PostAsJsonAsync("/api/v1/sources", new
        {
            name = "Test App Insights",
            type = "AppInsights",
            config = new { appId = "app-id", apiKey = "api-key" },
        });
        var source = await response.Content.ReadFromJsonAsync<SourceResponse>();
        _sourceId = source!.Id;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    private async Task<AlertResponse> CreateAlertAsync(string name = "Test alert")
    {
        var response = await _client.PostAsJsonAsync("/api/v1/alerts", new
        {
            name,
            sourceId = _sourceId,
            query = "exceptions | where message contains \"boom\"",
            checkIntervalSeconds = 300,
            threshold = 1,
            enabled = true,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AlertResponse>(Json))!;
    }

    [Fact]
    public async Task GetAlerts_ReturnsEmptyList_WhenNoAlerts()
    {
        var response = await _client.GetAsync("/api/v1/alerts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var alerts = await response.Content.ReadFromJsonAsync<List<AlertResponse>>(Json);
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAlert_ReturnsCreated_WithDefaults()
    {
        var alert = await CreateAlertAsync();

        alert.Id.Should().NotBeEmpty();
        alert.Name.Should().Be("Test alert");
        alert.SourceId.Should().Be(_sourceId);
        alert.SourceName.Should().Be("Test App Insights");
        alert.Status.Should().Be(AlertStatus.Ok);
        alert.ResolvedAt.Should().BeNull();
        alert.LastCheckedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateAlert_ReturnsValidation_WhenIntervalBelowMinimum()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/alerts", new
        {
            name = "Too fast",
            sourceId = _sourceId,
            query = "exceptions",
            checkIntervalSeconds = 30,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAlert_ReturnsValidation_WhenQueryAndTagMissing()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/alerts", new
        {
            name = "No condition",
            sourceId = _sourceId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateAlert_ReturnsNotFound_WhenSourceMissing()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/alerts", new
        {
            name = "Orphan",
            sourceId = Guid.NewGuid(),
            query = "exceptions",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateAlert_ReturnsNotFound_WhenTagMissing()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/alerts", new
        {
            name = "From missing tag",
            sourceId = _sourceId,
            tagId = Guid.NewGuid(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateAlert_FromTag_StoresFlattenedQueryWithoutLimit()
    {
        var tagResponse = await _client.PostAsJsonAsync("/api/v1/tags", new
        {
            name = "Prod errors",
            filters = new { terms = new[] { "prod-failure" }, levels = new[] { "error" }, eventTypes = new[] { "exception" } },
        });
        var tag = await tagResponse.Content.ReadFromJsonAsync<JsonElement>();
        var tagId = tag.GetProperty("id").GetGuid();

        var response = await _client.PostAsJsonAsync("/api/v1/alerts", new
        {
            name = "From tag",
            sourceId = _sourceId,
            tagId,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var alert = await response.Content.ReadFromJsonAsync<AlertResponse>(Json);
        alert!.Query.Should().Contain("prod-failure");
        alert.Query.Should().Contain("exceptions");
        alert.Query.Should().NotContain("| limit");
    }

    [Fact]
    public async Task UpdateAlert_ReturnsUpdatedAlert()
    {
        var created = await CreateAlertAsync("Original");

        var response = await _client.PutAsJsonAsync($"/api/v1/alerts/{created.Id}", new
        {
            name = "Updated",
            sourceId = _sourceId,
            query = "traces | where message contains \"slow\"",
            checkIntervalSeconds = 600,
            threshold = 5,
            enabled = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<AlertResponse>(Json);
        updated!.Name.Should().Be("Updated");
        updated.CheckIntervalSeconds.Should().Be(600);
        updated.Threshold.Should().Be(5);
        updated.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAlert_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.PutAsJsonAsync($"/api/v1/alerts/{Guid.NewGuid()}", new
        {
            name = "Nope",
            sourceId = _sourceId,
            query = "exceptions",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAlert_ReturnsNoContent_AndRemovesAlert()
    {
        var created = await CreateAlertAsync("To delete");

        var response = await _client.DeleteAsync($"/api/v1/alerts/{created.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await _client.GetFromJsonAsync<List<AlertResponse>>("/api/v1/alerts", Json);
        list.Should().NotContain(a => a.Id == created.Id);
    }

    [Fact]
    public async Task DeleteAlert_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.DeleteAsync($"/api/v1/alerts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ResolveAlert_SetsStatusOk_AndResolvedAt()
    {
        var created = await CreateAlertAsync("Firing alert");
        await SetStatusAsync(created.Id, AlertStatus.Firing);

        var response = await _client.PostAsync($"/api/v1/alerts/{created.Id}/resolve", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var resolved = await response.Content.ReadFromJsonAsync<AlertResponse>(Json);
        resolved!.Status.Should().Be(AlertStatus.Ok);
        resolved.ResolvedAt.Should().NotBeNull();
        resolved.ResolvedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ResolveAlert_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.PostAsync($"/api/v1/alerts/{Guid.NewGuid()}/resolve", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFirings_ReturnsNotFound_WhenAlertMissing()
    {
        var response = await _client.GetAsync($"/api/v1/alerts/{Guid.NewGuid()}/firings");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetFirings_ReturnsNewestFirst_CappedAt50()
    {
        var created = await CreateAlertAsync("Busy alert");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DashyDbContext>();
            var baseTime = DateTime.UtcNow.AddHours(-2);
            for (var i = 0; i < 60; i++)
            {
                db.AlertFirings.Add(new AlertFiring
                {
                    Id = Guid.NewGuid(),
                    AlertId = created.Id,
                    FiredAt = baseTime.AddMinutes(i),
                    ResultCount = i + 1,
                });
            }
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/v1/alerts/{created.Id}/firings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var firings = await response.Content.ReadFromJsonAsync<List<AlertFiringResponse>>(Json);
        firings.Should().HaveCount(50);
        firings.Should().BeInDescendingOrder(f => f.FiredAt);
        firings![0].ResultCount.Should().Be(60);
    }

    [Fact]
    public async Task Stream_SendsEventStreamPreamble()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        using var response = await _client.GetAsync(
            "/api/v1/alerts/stream", HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        var buffer = new byte[64];
        var read = await stream.ReadAsync(buffer, cts.Token);
        var preamble = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
        preamble.Should().StartWith("retry: 3000");
    }

    private async Task SetStatusAsync(Guid alertId, AlertStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DashyDbContext>();
        var alert = await db.Alerts.FindAsync(alertId);
        alert!.Status = status;
        await db.SaveChangesAsync();
    }
}
