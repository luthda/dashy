using Dashy.Api.Data.Configurations;
using Dashy.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dashy.Api.Data;

public class DashyDbContext(DbContextOptions<DashyDbContext> options) : DbContext(options)
{
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<AlertFiring> AlertFirings => Set<AlertFiring>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SourceConfiguration());
        modelBuilder.ApplyConfiguration(new TagConfiguration());
        modelBuilder.ApplyConfiguration(new SavedSearchConfiguration());
        modelBuilder.ApplyConfiguration(new AlertConfiguration());
        modelBuilder.ApplyConfiguration(new AlertFiringConfiguration());
    }
}
