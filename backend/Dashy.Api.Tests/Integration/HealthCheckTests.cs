using Dashy.Api.Tests.Infrastructure;
using FluentAssertions;

namespace Dashy.Api.Tests.Integration;

public class HealthCheckTests : IAsyncLifetime
{
    private readonly DashyWebApplicationFactory _factory = new();

    public async Task InitializeAsync() => await _factory.InitialiseDatabaseAsync();
    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Healthz_Returns200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/healthz");
        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
