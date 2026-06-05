using Dashy.Api.Domain.Entities;
using Dashy.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dashy.Api.Application.Services;

public class SavedSearchService(DashyDbContext db, ILogger<SavedSearchService> logger)
{
    public async Task<List<SavedSearch>> GetAllAsync(CancellationToken ct)
    {
        return await db.SavedSearches.OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
    }

    public async Task<SavedSearch> CreateAsync(CreateSavedSearchRequest request, CancellationToken ct)
    {
        logger.LogInformation("Creating saved search {Name}", request.Name);

        var search = new SavedSearch
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Query = request.Query ?? "",
            CreatedAt = DateTime.UtcNow,
        };

        db.SavedSearches.Add(search);
        await db.SaveChangesAsync(ct);
        return search;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        logger.LogInformation("Deleting saved search {Id}", id);

        var search = await db.SavedSearches.FindAsync([id], ct);
        if (search is null)
        {
            return false;
        }

        db.SavedSearches.Remove(search);
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public record CreateSavedSearchRequest(string Name, string? Query);

public record SavedSearchResponse(Guid Id, string Name, string Query, DateTime CreatedAt);
