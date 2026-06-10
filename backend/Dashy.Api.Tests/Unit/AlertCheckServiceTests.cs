using Dashy.Api.Application.Abstractions;
using Dashy.Api.Application.Services;
using Dashy.Api.Domain.Entities;
using Dashy.Api.Domain.Models;
using Dashy.Api.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using LogEntry = Dashy.Api.Domain.Models.LogEntry;

namespace Dashy.Api.Tests.Unit;

public class AlertCheckServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DashyDbContext _db;
    private readonly FakeLogSourceAdapter _adapter = new();
    private readonly FakeBroadcaster _broadcaster = new();
    private readonly AlertCheckService _sut;
    private readonly Source _source;

    public AlertCheckServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DashyDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new DashyDbContext(options);
        _db.Database.EnsureCreated();

        _source = new Source
        {
            Id = Guid.NewGuid(),
            Name = "Test source",
            Type = SourceType.AppInsights,
            Config = """{"appId":"a","apiKey":"k"}""",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Sources.Add(_source);
        _db.SaveChanges();

        var sourceService = new SourceService(
            _db,
            new PassthroughEncryptionService(),
            new FakeAdapterFactory(_adapter),
            NullLogger<SourceService>.Instance);

        _sut = new AlertCheckService(
            _db,
            sourceService,
            new FakeAdapterFactory(_adapter),
            _broadcaster,
            NullLogger<AlertCheckService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Alert SeedAlert(AlertStatus status = AlertStatus.Ok, DateTime? resolvedAt = null)
    {
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            Name = "Test alert",
            SourceId = _source.Id,
            Source = _source,
            Query = "exceptions",
            CheckIntervalSeconds = 300,
            Threshold = 2,
            Enabled = true,
            Status = status,
            ResolvedAt = resolvedAt,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Alerts.Add(alert);
        _db.SaveChanges();
        return alert;
    }

    [Fact]
    public async Task CheckAsync_SetsFiring_AndInsertsFiring_WhenCountMeetsThreshold()
    {
        var alert = SeedAlert();
        _adapter.CountResult = 5;
        var now = DateTime.UtcNow;

        await _sut.CheckAsync(alert, now, CancellationToken.None);

        alert.Status.Should().Be(AlertStatus.Firing);
        alert.LastCheckedAt.Should().Be(now);

        var firings = await _db.AlertFirings.Where(f => f.AlertId == alert.Id).ToListAsync();
        firings.Should().ContainSingle();
        firings[0].ResultCount.Should().Be(5);
        firings[0].FiredAt.Should().Be(now);
    }

    [Fact]
    public async Task CheckAsync_BroadcastsEvent_WhenFiring()
    {
        var alert = SeedAlert();
        _adapter.CountResult = 3;
        var now = DateTime.UtcNow;

        await _sut.CheckAsync(alert, now, CancellationToken.None);

        _broadcaster.Events.Should().ContainSingle();
        _broadcaster.Events[0].AlertId.Should().Be(alert.Id);
        _broadcaster.Events[0].AlertName.Should().Be("Test alert");
        _broadcaster.Events[0].ResultCount.Should().Be(3);
    }

    [Fact]
    public async Task CheckAsync_ClearsResolvedAt_OnReFire()
    {
        var alert = SeedAlert(status: AlertStatus.Ok, resolvedAt: DateTime.UtcNow.AddMinutes(-10));
        _adapter.CountResult = 2;

        await _sut.CheckAsync(alert, DateTime.UtcNow, CancellationToken.None);

        alert.Status.Should().Be(AlertStatus.Firing);
        alert.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_SetsOk_AndKeepsResolvedAt_OnCleanPoll()
    {
        var resolvedAt = DateTime.UtcNow.AddMinutes(-10);
        var alert = SeedAlert(status: AlertStatus.Firing, resolvedAt: resolvedAt);
        _adapter.CountResult = 1; // below threshold of 2

        await _sut.CheckAsync(alert, DateTime.UtcNow, CancellationToken.None);

        alert.Status.Should().Be(AlertStatus.Ok);
        alert.ResolvedAt.Should().Be(resolvedAt);
        _broadcaster.Events.Should().BeEmpty();
        (await _db.AlertFirings.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CheckAsync_SetsError_WhenAdapterThrows()
    {
        var alert = SeedAlert();
        _adapter.ThrowOnCount = new InvalidOperationException("source unreachable");
        var now = DateTime.UtcNow;

        await _sut.CheckAsync(alert, now, CancellationToken.None);

        alert.Status.Should().Be(AlertStatus.Error);
        alert.LastCheckedAt.Should().Be(now);
        _broadcaster.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckAsync_QueriesWindowOfOneCheckInterval()
    {
        var alert = SeedAlert();
        _adapter.CountResult = 0;
        var now = DateTime.UtcNow;

        await _sut.CheckAsync(alert, now, CancellationToken.None);

        _adapter.LastCountRequest.Should().NotBeNull();
        _adapter.LastCountRequest!.From.Should().Be(now.AddSeconds(-alert.CheckIntervalSeconds));
        _adapter.LastCountRequest.To.Should().Be(now);
        _adapter.LastCountRequest.BaseQuery.Should().Be("exceptions");
    }

    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class PassthroughEncryptionService : IEncryptionService
    {
        public string Encrypt(string plaintext) => plaintext;
        public string Decrypt(string ciphertext) => ciphertext;
    }

    private sealed class FakeLogSourceAdapter : ILogSourceAdapter
    {
        public int CountResult { get; set; }
        public Exception? ThrowOnCount { get; set; }
        public AdapterCountRequest? LastCountRequest { get; private set; }

        public SourceType SourceType => SourceType.AppInsights;

        public Task<int> CountAsync(AdapterCountRequest request, CancellationToken ct)
        {
            LastCountRequest = request;
            if (ThrowOnCount is not null)
            {
                throw ThrowOnCount;
            }

            return Task.FromResult(CountResult);
        }

        public Task<List<LogEntry>> QueryAsync(AdapterQueryRequest request, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task TestConnectionAsync(string configJson, string sourceName, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class FakeAdapterFactory(ILogSourceAdapter adapter) : ILogSourceAdapterFactory
    {
        public ILogSourceAdapter GetAdapter(SourceType type) => adapter;
    }

    private sealed class FakeBroadcaster : IAlertBroadcaster
    {
        public List<AlertFiredEvent> Events { get; } = [];

        public Task BroadcastAsync(AlertFiredEvent evt, CancellationToken ct)
        {
            Events.Add(evt);
            return Task.CompletedTask;
        }
    }
}
