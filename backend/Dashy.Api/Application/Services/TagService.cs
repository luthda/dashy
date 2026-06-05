using System.Text.Json;
using Dashy.Api.Domain.Entities;
using Dashy.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dashy.Api.Application.Services;

public class TagService(DashyDbContext db, ILogger<TagService> logger)
{
    public async Task<List<Tag>> GetAllAsync(CancellationToken ct)
    {
        return await db.Tags.OrderBy(t => t.CreatedAt).ToListAsync(ct);
    }

    public async Task<Tag> CreateAsync(CreateTagRequest request, CancellationToken ct)
    {
        logger.LogInformation("Creating tag {Name}", request.Name);

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Color = request.Color ?? "#6366f1",
            Filters = JsonSerializer.Serialize(request.Filters ?? new TagFiltersDto()),
            CreatedAt = DateTime.UtcNow,
        };

        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);
        return tag;
    }

    public async Task<Tag?> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken ct)
    {
        logger.LogInformation("Updating tag {Id}", id);

        var tag = await db.Tags.FindAsync([id], ct);
        if (tag is null)
        {
            return null;
        }

        tag.Name = request.Name ?? tag.Name;
        tag.Color = request.Color ?? tag.Color;

        if (request.Filters is not null)
        {
            tag.Filters = JsonSerializer.Serialize(request.Filters);
        }

        await db.SaveChangesAsync(ct);
        return tag;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        logger.LogInformation("Deleting tag {Id}", id);

        var tag = await db.Tags.FindAsync([id], ct);
        if (tag is null)
        {
            return false;
        }

        db.Tags.Remove(tag);
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public record TagFiltersDto(
    List<string>? Terms = null,
    List<string>? Levels = null,
    List<string>? EventTypes = null);

public record CreateTagRequest(
    string Name,
    string? Color,
    TagFiltersDto? Filters);

public record UpdateTagRequest(
    string? Name,
    string? Color,
    TagFiltersDto? Filters);
