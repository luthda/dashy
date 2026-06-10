using Dashy.Api.Application.Abstractions;
using Dashy.Api.Application.Services;
using Dashy.Api.Controllers;
using Dashy.Api.Infrastructure.Encryption;
using Dashy.Api.Infrastructure.LogSources;
using Dashy.Api.Infrastructure.Persistence;
using Dashy.Api.Options;
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
    {
        builder.Configuration["Encryption:Key"] = envKey;
    }
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
builder.Services.AddTransient<ILogSourceAdapterFactory, LogSourceAdapterFactory>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<SourceService>();
builder.Services.AddScoped<LogQueryService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<SavedSearchService>();

// ── JSON / API ────────────────────────────────────────────────────────────────
builder.Services.ConfigureHttpJsonOptions(opt =>
{
    opt.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    // Accept/emit enums as their string names (e.g. "AppInsights")
    opt.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
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

app.MapGroup("/api/v1/sources").MapLogSourceEndpoints();
app.MapGroup("/api/v1/logs").MapLogEndpoints();
app.MapGroup("/api/v1/tags").MapTagEndpoints();
app.MapGroup("/api/v1/saved-searches").MapSavedSearchEndpoints();

app.Run();

// Marker for WebApplicationFactory
public partial class Program { }
