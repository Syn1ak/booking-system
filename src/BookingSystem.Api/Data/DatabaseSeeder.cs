using BookingSystem.Api.Authorization;
using Microsoft.AspNetCore.Identity;

namespace BookingSystem.Api.Data;

public static class DatabaseSeeder
{
    /// <summary>
    /// Creates any role that does not yet exist. Idempotent, so it is safe on every start.
    /// Registration assigns a role, and Identity rejects assignment of a role that is
    /// absent, so this must run before the first request.
    /// </summary>
    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }
}
