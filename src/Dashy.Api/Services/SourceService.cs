using System.Text.Json;
using Dashy.Api.Data;
using Dashy.Api.Data.Entities;
using Dashy.Api.Infrastructure;
using Dashy.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Dashy.Api.Services;

public class SourceService(
    DashyDbContext db,
    IEncryptionService encryption,
    AppInsightsClient appInsights,
    LokiClient loki,
    ILogger<SourceService> logger)
{
    public async Task<List<Source>> GetAllAsync(CancellationToken ct)
    {
        logger.LogDebug("Listing all sources");
        return await db.Sources.OrderBy(s => s.CreatedAt).ToListAsync(ct);
    }

    public async Task<Source?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        logger.LogDebug("Getting source {Id}", id);
        return await db.Sources.FindAsync([id], ct);
    }

    public async Task<Source> CreateAsync(CreateSourceRequest request, CancellationToken ct)
    {
        logger.LogInformation("Creating source {Name} type={Type}", request.Name, request.Type);

        var configJson = JsonSerializer.Serialize(request.Config);
        var source = new Source
        {
            Id        = Guid.NewGuid(),
            Name      = request.Name,
            Type      = request.Type,
            Config    = encryption.Encrypt(configJson),
            CreatedAt = DateTime.UtcNow,
        };

        db.Sources.Add(source);
        await db.SaveChangesAsync(ct);
        return source;
    }

    public async Task<Source?> UpdateAsync(Guid id, UpdateSourceRequest request, CancellationToken ct)
    {
        logger.LogInformation("Updating source {Id}", id);

        var source = await db.Sources.FindAsync([id], ct);
        if (source is null) return null;

        source.Name = request.Name ?? source.Name;

        if (request.Config is not null)
        {
            var configJson = JsonSerializer.Serialize(request.Config);
            source.Config = encryption.Encrypt(configJson);
        }

        await db.SaveChangesAsync(ct);
        return source;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        logger.LogInformation("Deleting source {Id}", id);

        var source = await db.Sources.FindAsync([id], ct);
        if (source is null) return false;

        db.Sources.Remove(source);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(Guid id, CancellationToken ct)
    {
        logger.LogInformation("Testing connection for source {Id}", id);

        var source = await db.Sources.FindAsync([id], ct);
        if (source is null)
            return new ConnectionTestResult(false, "Source not found");

        try
        {
            var configJson = encryption.Decrypt(source.Config);

            return source.Type switch
            {
                SourceType.AppInsights => await TestAppInsightsAsync(configJson, source.Name, ct),
                SourceType.Loki        => await TestLokiAsync(configJson, source.Name, ct),
                _                      => new ConnectionTestResult(false, $"Unknown source type: {source.Type}"),
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Connection test failed for source {Id}", id);
            return new ConnectionTestResult(false, ex.Message);
        }
    }

    private async Task<ConnectionTestResult> TestAppInsightsAsync(
        string configJson, string sourceName, CancellationToken ct)
    {
        var cfg = JsonSerializer.Deserialize<AppInsightsConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid App Insights config");

        await appInsights.QueryAsync(cfg.AppId, cfg.ApiKey, "traces | limit 1", null, sourceName, ct);
        return new ConnectionTestResult(true);
    }

    private async Task<ConnectionTestResult> TestLokiAsync(
        string configJson, string sourceName, CancellationToken ct)
    {
        var cfg = JsonSerializer.Deserialize<LokiConfig>(configJson)
            ?? throw new InvalidOperationException("Invalid Loki config");

        var now    = DateTimeOffset.UtcNow;
        var end    = (now.ToUnixTimeSeconds() * 1_000_000_000L).ToString();
        var start  = ((now.AddMinutes(-1).ToUnixTimeSeconds()) * 1_000_000_000L).ToString();

        await loki.QueryAsync(cfg.BaseUrl, cfg.OrgId, cfg.AuthToken, "{app=\"__test__\"}", start, end, 1, sourceName, ct);
        return new ConnectionTestResult(true);
    }

    /// <summary>Decrypts config and returns it. Never call from a response-bound path.</summary>
    public T DecryptConfig<T>(Source source) where T : class
    {
        var json = encryption.Decrypt(source.Config);
        return JsonSerializer.Deserialize<T>(json)
            ?? throw new InvalidOperationException($"Cannot deserialize config for source {source.Id}");
    }
}

public record ConnectionTestResult(bool Ok, string? Error = null);

public record CreateSourceRequest(
    string Name,
    SourceType Type,
    object Config);

public record UpdateSourceRequest(
    string? Name,
    object? Config);

// Config shapes (internal — never returned to browser)
public record AppInsightsConfig(string AppId, string ApiKey);
public record LokiConfig(string BaseUrl, string? OrgId, string? AuthToken);
