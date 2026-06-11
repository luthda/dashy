using Dashy.Api.Domain.Entities;
using Dashy.Api.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Dashy.Api.Infrastructure.Persistence;

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

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // All DateTimes are stored as UTC; restamp Kind=Utc on read (see UtcDateTimeConverter).
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
