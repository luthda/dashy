using Dashy.Api.Domain.Entities;
using Dashy.Api.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Dashy.Api.Tests.Integration;

/// <summary>
/// Production applies migrations on startup (not EnsureCreated), so this exercises the
/// full migration chain — including the hand-authored NarrowSavedSearches migration that
/// drops the legacy source link / tags / time-range columns from saved_searches.
/// </summary>
public class SavedSearchMigrationTests
{
    [Fact]
    public async Task Migrate_AppliesNarrowSavedSearches_AndAllowsInsert()
    {
        // Shared-cache in-memory DB kept alive by an open connection for the test's duration.
        var connectionString = $"Data Source=MigrateTest_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var keepAlive = new SqliteConnection(connectionString);
        await keepAlive.OpenAsync();

        var options = new DbContextOptionsBuilder<DashyDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using (var db = new DashyDbContext(options))
        {
            await db.Database.MigrateAsync();

            db.SavedSearches.Add(new SavedSearch
            {
                Id = Guid.NewGuid(),
                Name = "After migration",
                Query = "winfap",
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new DashyDbContext(options))
        {
            var search = await db.SavedSearches.SingleAsync();
            search.Name.Should().Be("After migration");
            search.Query.Should().Be("winfap");
        }
    }
}
