using System.Text.Json;
using Dashy.Api.Data;
using Dashy.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashy.Api.Services;

public class SourceService(
    DashyDbContext db,
    IEncryptionService encryption,
    ILogSourceAdapterFactory adapterFactory,
    ILogger<SourceService> logger)
{
    public async Task<List<Source>> GetAllAsync(CancellationToken ct)
    {
        return await db.Sources.OrderBy(s => s.CreatedAt).ToListAsync(ct);
    }

    public async Task<Source?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await db.Sources.FindAsync([id], ct);
    }

    public async Task<Source> CreateAsync(CreateSourceRequest request, CancellationToken ct)
    {
        logger.LogInformation("Creating source {Name} type={Type}", request.Name, request.Type);

        var configJson = JsonSerializer.Serialize(request.Config);
        var source = new Source
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            Config = encryption.Encrypt(configJson),
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
        if (source is null)
        {
            return null;
        }

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
        if (source is null)
        {
            return false;
        }

        db.Sources.Remove(source);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(Guid id, CancellationToken ct)
    {
        logger.LogInformation("Testing connection for source {Id}", id);

        var source = await db.Sources.FindAsync([id], ct);
        if (source is null)
        {
            return new ConnectionTestResult(false, "Source not found");
        }

        try
        {
            var configJson = encryption.Decrypt(source.Config);
            var adapter = adapterFactory.GetAdapter(source.Type);
            await adapter.TestConnectionAsync(configJson, source.Name, ct);
            return new ConnectionTestResult(true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Connection test failed for source {Id}", id);
            return new ConnectionTestResult(false, ex.Message);
        }
    }

    public string DecryptConfig(Source source)
    {
        return encryption.Decrypt(source.Config);
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

public record AppInsightsConfig(string AppId, string ApiKey);
