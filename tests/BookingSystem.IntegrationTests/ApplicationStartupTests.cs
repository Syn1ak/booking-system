using System.Net;
using BookingSystem.Api.Authorization;
using BookingSystem.Api.Data;
using BookingSystem.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookingSystem.IntegrationTests;

/// <summary>
/// Checks that the application comes up correctly against a real database, so that a failure
/// in a feature test points at the feature rather than at the infrastructure beneath it.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ApplicationStartupTests(ApiFactory factory)
{
    [Fact]
    public async Task GetHealth_WhenApplicationStarted_ReturnsOk()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Startup_OnFreshDatabase_AppliesMigrationsAndSeedsRoles()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var roles = await context.Roles.Select(role => role.Name).ToListAsync();

        Assert.Equal(Roles.All.Order(), roles.Order());
    }
}
