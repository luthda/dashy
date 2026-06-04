using Dashy.Api.Data;
using Dashy.Api.Endpoints;
using Dashy.Api.Infrastructure;
using Dashy.Api.Options;
using Dashy.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Options ───────────────────────────────────────────────────────────────────
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.Section));

// Allow ENCRYPTION_KEY env var to override the config value
var encryptionKey = builder.Configuration["Encryption:Key"];
if (string.IsNullOrEmpty(encryptionKey))
{
    var envKey = Environment.GetEnvironmentVariable("ENCRYPTION_KEY");
    if (!string.IsNullOrEmpty(envKey))
        builder.Configuration["Encryption:Key"] = envKey;
}

builder.Services.Configure<EncryptionOptions>(
    builder.Configuration.GetSection(EncryptionOptions.Section));

// ── Database ──────────────────────────────────────────────────────────────────
var connectionString = builder.Configuration
    .GetSection(DatabaseOptions.Section)
    .GetValue<string>("ConnectionString")
    ?? "Data Source=/data/dashy.db";

builder.Services.AddDbContext<DashyDbContext>(opt =>
    opt.UseSqlite(connectionString));

// ── Infrastructure ────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IEncryptionService, AesGcmEncryptionService>();

// ── Log source adapters ──────────────────────────────────────────────────────
builder.Services.AddHttpClient<AppInsightsAdapter>();
builder.Services.AddHttpClient<LokiAdapter>();
builder.Services.AddTransient<ILogSourceAdapterFactory, LogSourceAdapterFactory>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<SourceService>();
builder.Services.AddScoped<LogQueryService>();

// ── JSON / API ────────────────────────────────────────────────────────────────
builder.Services.ConfigureHttpJsonOptions(opt =>
{
    opt.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// ── CORS (for Vite dev server) ────────────────────────────────────────────────
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

// ── Auto-migrate on startup ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DashyDbContext>();
    db.Database.Migrate();
}

app.UseCors();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).WithName("Health");

app.MapGroup("/api/v1/sources").MapSourceEndpoints();
app.MapGroup("/api/v1/logs").MapLogEndpoints();

app.Run();

// Marker for WebApplicationFactory
public partial class Program { }
