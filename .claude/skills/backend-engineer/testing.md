# Testing Patterns

Reference for xUnit tests, `WebApplicationFactory`, and test conventions.

---

## Test Project Structure

```
Dashy.Api.Tests/
  Infrastructure/
    DashyWebApplicationFactory.cs     # Shared factory, in-memory SQLite
  Integration/
    AlertEndpointTests.cs
    TagEndpointTests.cs
    SavedSearchEndpointTests.cs
    HealthCheckTests.cs
  Unit/
    AlertCheckServiceTests.cs
    EncryptionServiceTests.cs
    KqlBuilderTests.cs
```

---

## WebApplicationFactory

Uses **in-memory SQLite with a shared-cache keep-alive connection** — not Testcontainers.
A single `SqliteConnection` is held open for the factory lifetime so the shared-cache
in-memory database persists across multiple DI scopes.

```csharp
public class DashyWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        $"Data Source=DashyTest_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    // Keep one connection open so the in-memory DB isn't destroyed between scopes.
    private readonly SqliteConnection _keepAlive;

    public DashyWebApplicationFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = Convert.ToBase64String(new byte[32]),
                ["Database:ConnectionString"] = _connectionString,
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DashyDbContext>>();
            services.RemoveAll<DashyDbContext>();
            services.AddDbContext<DashyDbContext>(opt => opt.UseSqlite(_connectionString));
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitialiseDatabaseAsync()
    {
        await _keepAlive.OpenAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DashyDbContext>();
        await db.Database.EnsureCreatedAsync();   // ← EnsureCreated, not Migrate
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _keepAlive.Dispose();
    }
}
```

---

## Integration Test Base

Tests implement `IAsyncLifetime` directly (not `IClassFixture`) and create a fresh factory
per test class. `InitialiseDatabaseAsync` is called in `InitializeAsync`.

```csharp
public class AlertEndpointTests : IAsyncLifetime
{
    private readonly DashyWebApplicationFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.InitialiseDatabaseAsync();
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task GetAlerts_ReturnsEmptyList_WhenNoAlerts()
    {
        var response = await _client.GetAsync("/api/v1/alerts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var alerts = await response.Content.ReadFromJsonAsync<List<AlertResponse>>(Json);
        alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAlert_ReturnsNotFound_WhenMissing()
    {
        var response = await _client.DeleteAsync($"/api/v1/alerts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

To seed data directly, resolve `DashyDbContext` from the factory's service provider:

```csharp
using var scope = _factory.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<DashyDbContext>();
db.AlertFirings.Add(new AlertFiring { ... });
await db.SaveChangesAsync();
```

---

## Assertion Library

Use **FluentAssertions** for readable assertions.

```csharp
response.StatusCode.Should().Be(HttpStatusCode.OK);
body.Should().NotBeNull();
body!.Name.Should().Be("expected");
body.Items.Should().HaveCount(3);
body.Items.Should().BeInDescendingOrder(f => f.FiredAt);
```

---

## Service Unit Tests

For testing service logic in isolation, create an in-memory SQLite `DashyDbContext` directly.
Use `IDisposable` (not `IAsyncLifetime`) — keep-alive connection held for the test class.

```csharp
public class AlertCheckServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DashyDbContext _db;
    private readonly FakeLogSourceAdapter _adapter = new();
    private readonly FakeBroadcaster _broadcaster = new();
    private readonly AlertCheckService _sut;

    public AlertCheckServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DashyDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new DashyDbContext(options);
        _db.Database.EnsureCreated();

        var sourceService = new SourceService(
            _db, new PassthroughEncryptionService(),
            new FakeAdapterFactory(_adapter),
            NullLogger<SourceService>.Instance);

        _sut = new AlertCheckService(
            _db, sourceService,
            new FakeAdapterFactory(_adapter),
            _broadcaster,
            NullLogger<AlertCheckService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CheckAsync_SetsFiring_WhenCountMeetsThreshold()
    {
        var alert = SeedAlert();
        _adapter.CountResult = 5;

        await _sut.CheckAsync(alert, DateTime.UtcNow, CancellationToken.None);

        alert.Status.Should().Be(AlertStatus.Firing);
        (await _db.AlertFirings.CountAsync()).Should().Be(1);
    }
}
```

**Fake infrastructure**: declare fakes as private sealed inner classes in the test file.
Use `IEncryptionService` passthrough (returns plaintext), fake adapter, fake broadcaster.
Don't use Moq or NSubstitute unless the interface is too complex to fake by hand.

---

## Rules

1. **Affected tests only** — only write or update tests for code you changed. State which
   tests are affected: *"Affected tests: AlertEndpointTests.CreateAlert_ReturnsCreated"*

2. **In-memory SQLite** — integration tests use the `DashyWebApplicationFactory` (shared-cache
   in-memory SQLite). Unit tests open their own `:memory:` connection directly. No Testcontainers.

3. **No mocking DbContext** — test against a real (in-memory) SQLite database. Mock only
   external services (`ILogSourceAdapter`, `IAlertBroadcaster`, `IEncryptionService`).

4. **One assertion focus per test** — a test can have multiple assertions, but they should all
   verify one logical outcome.

5. **Test names** — `MethodName_ExpectedResult_WhenCondition` pattern:
   - `CreateAlert_ReturnsCreated_WithDefaults`
   - `DeleteAlert_ReturnsNotFound_WhenMissing`
   - `CheckAsync_SetsFiring_WhenCountMeetsThreshold`

6. **Cleanup** — each test class gets its own factory instance → isolated database per class.
   Tests within a class share the same in-memory DB, so seed and assert within the same test
   or use helper methods that are self-contained.
