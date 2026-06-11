# Data Access Patterns

Reference for EF Core entities, DbContext configuration, queries, and migrations.

---

## DbContext

Single `DashyDbContext` with a `DbSet<T>` per entity. Registered as a scoped service.

```csharp
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
```

Registration in `Program.cs`:

```csharp
builder.Services.AddDbContext<DashyDbContext>(options =>
    options.UseSqlite(connectionString));
```

---

## Entities

Entities are mutable classes in `Data/Entities/`. Use `required` properties where appropriate.
Navigation properties use `= [];` for collections.

```csharp
public class Source
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required SourceType Type { get; set; }
    public required string EncryptedConfig { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public List<SavedSearch> SavedSearches { get; set; } = [];
    public List<Alert> Alerts { get; set; } = [];
}

public enum SourceType
{
    AppInsights,
    Loki
}
```

---

## Entity Configuration

One `IEntityTypeConfiguration<T>` per entity in `Infrastructure/Persistence/Configurations/`. This is where
table names, column names, constraints, defaults, and relationships live — not on the entity.

```csharp
public class SourceConfiguration : IEntityTypeConfiguration<Source>
{
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder.ToTable("sources");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.Name).HasColumnName("name").IsRequired();
        builder.Property(s => s.Type).HasColumnName("type").HasConversion<string>().IsRequired();
        builder.Property(s => s.EncryptedConfig).HasColumnName("config").HasColumnType("text").IsRequired();
        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("datetime('now')");
    }
}
```

Naming rules:
- Table names: `snake_case`, plural (e.g. `sources`, `saved_searches`, `alert_firings`)
- Column names: `snake_case` (e.g. `created_at`, `source_id`)
- Enum storage: string conversion (`.HasConversion<string>()`)
- IDs: `Guid` type, generated in application code (`Guid.NewGuid()`) — SQLite has no UUID function
- Datetime default: `HasDefaultValueSql("datetime('now')")` — not `now()` (PostgreSQL syntax)

---

## JSON Columns (SQLite TEXT)

SQLite has no native JSON type. Complex structured data is stored as a plain `TEXT` column
containing a JSON string. The entity property is `string`; serialisation is handled in the
application layer.

```csharp
// Entity — plain string property
public class Tag
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Color { get; set; }
    public required string Filters { get; set; }  // JSON string, e.g. {"terms":[],"levels":[]}
    public DateTime CreatedAt { get; set; }
}

// Configuration — plain column, no OwnsOne/ToJson
builder.Property(t => t.Filters)
    .HasColumnName("filters")
    .IsRequired()
    .HasDefaultValue("{}");
```

Deserialise in the service layer when you need to work with the structured value:

```csharp
var filters = TagFilters.FromJson(tag.Filters) ?? TagFilters.Empty;
```

---

## Queries

Use async EF Core LINQ queries. Always pass `CancellationToken`.

```csharp
// List with includes
var sources = await db.Sources
    .OrderBy(s => s.Name)
    .ToListAsync(ct);

// Single by ID — throw if not found
var alert = await db.Alerts
    .Include(a => a.Source)
    .FirstOrDefaultAsync(a => a.Id == alertId, ct)
    ?? throw new AlertNotFoundException(alertId);

// Filtered query
var firings = await db.AlertFirings
    .Where(f => f.AlertId == alertId)
    .OrderByDescending(f => f.FiredAt)
    .Take(100)
    .ToListAsync(ct);
```

Rules:
- Use `FirstOrDefaultAsync` + null check for single-entity lookups.
- Use `Include()` only when the navigation is needed by the caller — don't eager-load by default.
- Use `AsNoTracking()` for read-only queries where the entity won't be modified.
- Avoid raw SQL unless EF Core can't express the query.

---

## Creating & Updating

```csharp
// Create
var source = new Source
{
    Name = request.Name,
    Type = request.Type,
    EncryptedConfig = encryptionService.Encrypt(request.Config)
};
db.Sources.Add(source);
await db.SaveChangesAsync(ct);

// Update — load, mutate, save
var alert = await db.Alerts.FindAsync([alertId], ct)
    ?? throw new AlertNotFoundException(alertId);
alert.Name = request.Name;
alert.Query = request.Query;
alert.CheckIntervalSeconds = request.CheckIntervalSeconds;
alert.Threshold = request.Threshold;
alert.Enabled = request.Enabled;
await db.SaveChangesAsync(ct);
```

---

## Deleting

```csharp
var source = await db.Sources.FindAsync([sourceId], ct)
    ?? throw new NotFoundException($"Source {sourceId} not found");
db.Sources.Remove(source);
await db.SaveChangesAsync(ct);
```

Configure cascade deletes in entity configuration where appropriate (e.g. deleting a source
cascades to its saved searches and alerts).

---

## Migrations

Generate migrations from the project root:

```bash
dotnet ef migrations add <DescriptiveName> --project src/Dashy.Api
```

Rules:
- Never edit an existing migration after it has been applied.
- One migration per schema change — don't batch unrelated changes.
- Descriptive names: `AddSourcesTable`, `AddAlertStatusColumn`, `CreateAlertFiringsTable`.
- Review the generated migration for correctness before applying.
- Apply with: `dotnet ef database update --project src/Dashy.Api`

---

## Transactions

EF Core wraps `SaveChangesAsync()` in a transaction by default. For multi-step operations
that need an explicit transaction:

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(ct);
try
{
    // multiple operations
    await db.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
}
catch
{
    await transaction.RollbackAsync(ct);
    throw;
}
```

Only use explicit transactions when multiple `SaveChangesAsync` calls must be atomic.
