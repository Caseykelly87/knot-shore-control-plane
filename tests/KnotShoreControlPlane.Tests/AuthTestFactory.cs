using KnotShoreControlPlane.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KnotShoreControlPlane.Tests;

// Hosts the real application but swaps the Npgsql DbContext for an in-memory SQLite
// connection kept open for the host's lifetime, and supplies a test-only JWT signing
// key via in-memory configuration (never a committed secret).
public class AuthTestFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "test-only-signing-key-not-a-secret-0123456789ABCDEF";
    public const string Issuer = "knot-shore-control-plane-tests";
    public const string Audience = "knot-shore-control-plane-tests";

    private readonly SqliteConnection _connection = new("Filename=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = SigningKey,
                ["Jwt:Issuer"] = Issuer,
                ["Jwt:Audience"] = Audience,
                ["Jwt:AccessTokenMinutes"] = "60",
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<ControlPlaneDbContext>));
            services.Remove(descriptor);

            services.AddDbContext<ControlPlaneDbContext>(options => options.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
