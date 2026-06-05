using Dashy.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dashy.Api.Tests.Infrastructure;

public class DashyWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        $"Data Source=DashyTest_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    // Keep one connection open for the lifetime of the factory so the
    // shared-cache in-memory database is not destroyed between scopes.
    private readonly SqliteConnection _keepAlive;

    public DashyWebApplicationFactory()
    {
        _keepAlive = new SqliteConnection(_connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = Convert.ToBase64String(new byte[32]),
                ["Database:ConnectionString"] = _connectionString,
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DashyDbContext>>();
            services.RemoveAll<DashyDbContext>();

            services.AddDbContext<DashyDbContext>(opt =>
                opt.UseSqlite(_connectionString));
        });

        builder.UseEnvironment("Testing");
    }

    /// <summary>Opens the keep-alive connection and ensures the schema is applied.</summary>
    public async Task InitialiseDatabaseAsync()
    {
        await _keepAlive.OpenAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DashyDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keepAlive.Dispose();
        }
    }
}
