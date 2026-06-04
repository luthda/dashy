# Testing Patterns

Reference for xUnit tests, `WebApplicationFactory`, Testcontainers, and test conventions.

---

## Test Project Structure

```
src/Dashy.Api.Tests/
  Integration/
    Endpoints/
      SourceEndpointTests.cs
      TagEndpointTests.cs
      AlertEndpointTests.cs
      LogEndpointTests.cs
      SavedSearchEndpointTests.cs
    Services/
      SourceServiceTests.cs
      AlertPollingServiceTests.cs
  Fixtures/
    DashyWebApplicationFactory.cs
    PostgresFixture.cs
  Helpers/
    HttpClientExtensions.cs
```

---

## WebApplicationFactory

Custom factory that swaps in a Testcontainers PostgreSQL instance.

```csharp
public class DashyWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<DashyDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            // Add Testcontainers PostgreSQL
            services.AddDbContext<DashyDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
```

---

## Integration Test Base

Tests use `IClassFixture<DashyWebApplicationFactory>` to share the container across tests
in a class. Each test gets a fresh database state via migration + cleanup.

```csharp
public class SourceEndpointTests(DashyWebApplicationFactory factory)
    : IClassFixture<DashyWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateSource_ReturnsCreated()
    {
        var request = new { name = "Test Source", type = "loki", config = new { baseUrl = "http://loki:3100" } };

        var response = await _client.PostAsJsonAsync("/api/v1/sources", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<SourceResponse>();
        body!.Name.Should().Be("Test Source");
        body.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSources_ReturnsEmptyList_WhenNoSources()
    {
        var response = await _client.GetAsync("/api/v1/sources");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<SourceResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteSource_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.DeleteAsync($"/api/v1/sources/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

---

## Assertion Library

Use **FluentAssertions** for readable assertions.

```csharp
response.StatusCode.Should().Be(HttpStatusCode.OK);
body.Should().NotBeNull();
body!.Name.Should().Be("expected");
body.Items.Should().HaveCount(3);
body.Items.Should().Contain(x => x.Name == "Foo");
```

---

## Service Unit Tests

For testing service logic without HTTP, instantiate the service directly with a real (in-memory
or Testcontainers) `DashyDbContext` and mocked external dependencies.

```csharp
public class AlertServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .Build();
    private DashyDbContext _db = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<DashyDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        _db = new DashyDbContext(options);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task CreateAlert_PersistsToDatabase()
    {
        var service = new AlertService(_db, NullLogger<AlertService>.Instance);
        var request = new CreateAlertRequest("Test", sourceId, "error", 300, 5);

        var alert = await service.CreateAsync(request, CancellationToken.None);

        var persisted = await _db.Alerts.FindAsync(alert.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("Test");
    }
}
```

---

## Test Data Helpers

Create helper methods for common test data setup. Keep them in `Helpers/TestDataFactory.cs`.

```csharp
public static class TestDataFactory
{
    public static Source CreateSource(string name = "Test Source", SourceType type = SourceType.Loki)
        => new()
        {
            Name = name,
            Type = type,
            EncryptedConfig = "encrypted-test-config"
        };

    public static Alert CreateAlert(Guid sourceId, string name = "Test Alert")
        => new()
        {
            Name = name,
            SourceId = sourceId,
            Query = "error",
            CheckIntervalSeconds = 300,
            Threshold = 5,
            Enabled = true,
            Status = AlertStatus.Ok
        };
}
```

---

## Rules

1. **Affected tests only** — only write or update tests for code you changed. State which
   tests are affected: *"Affected tests: SourceEndpointTests.CreateSource_ReturnsCreated"*

2. **Real database** — use Testcontainers PostgreSQL for integration tests. No in-memory
   database provider — it doesn't match PostgreSQL behaviour (JSONB, UUID, etc.).

3. **No mocking DbContext** — test against a real database. Mock external services
   (`IHttpClientFactory`, external API clients) when needed.

4. **One assertion focus per test** — a test can have multiple assertions, but they should all
   verify one logical outcome.

5. **Test names** — `MethodName_ExpectedResult_WhenCondition` pattern:
   - `CreateSource_ReturnsCreated`
   - `DeleteSource_ReturnsNotFound_WhenMissing`
   - `PollAlerts_FiresAlert_WhenThresholdExceeded`

6. **Cleanup** — if tests share a database instance, ensure isolation via per-test cleanup
   or separate database creation.
