using Dashy.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dashy.Api.Tests.Infrastructure;

public class DashyWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Inject a deterministic test encryption key (32 zero bytes — never use in production)
        builder.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = Convert.ToBase64String(new byte[32]),
                ["Database:ConnectionString"] = "Data Source=:memory:;Mode=Memory;Cache=Shared",
            }));

        builder.ConfigureServices(services =>
        {
            // Replace the real DB registration with an in-memory SQLite instance
            services.RemoveAll<DbContextOptions<DashyDbContext>>();
            services.RemoveAll<DashyDbContext>();

            services.AddDbContext<DashyDbContext>(opt =>
                opt.UseSqlite("Data Source=:memory:;Mode=Memory;Cache=Shared"));
        });

        builder.UseEnvironment("Testing");
    }

    /// <summary>Creates a scope and ensures the SQLite schema is applied.</summary>
    public async Task InitialiseDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DashyDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}
