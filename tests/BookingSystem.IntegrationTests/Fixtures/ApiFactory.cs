using BookingSystem.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;

namespace BookingSystem.IntegrationTests.Fixtures;

/// <summary>
/// Boots the real application against a throwaway SQL Server container. Shared by every test
/// class in the assembly, so the container starts once rather than per class.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Pinned to the same image as compose.yaml, so tests and local development run
    // against the same SQL Server version rather than drifting apart silently.
    private readonly MsSqlContainer _database =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        // Migrations run before the host is ever started. Application startup seeds roles,
        // which queries tables that do not exist on a fresh container - so migrating from
        // inside the host would be too late.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_database.GetConnectionString())
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Supplied in memory rather than read from appsettings.Development.json, so tests
        // never depend on - or write to - the developer's local database.
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _database.GetConnectionString(),
                ["Jwt:Issuer"] = "BookingSystem.Tests",
                ["Jwt:Audience"] = "BookingSystem.Tests",
                ["Jwt:SigningKey"] = "integration-tests-signing-key-32-chars-minimum",
            }));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
