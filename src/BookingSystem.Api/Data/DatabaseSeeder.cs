using BookingSystem.Api.Authorization;
using BookingSystem.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BookingSystem.Api.Data;

public static class DatabaseSeeder
{
    /// <summary>
    /// Brings a database up to a usable state. Idempotent: every step checks before it
    /// writes, so this is safe on every start.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        await SeedRolesAsync(scope.ServiceProvider);
        await SeedUsersAsync(scope.ServiceProvider);
    }

    /// <summary>
    /// Registration assigns a role, and Identity rejects assignment of a role that is
    /// absent, so roles must exist before the first request.
    /// </summary>
    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole<Guid>(role));
            }
        }
    }

    private static async Task SeedUsersAsync(IServiceProvider services)
    {
        var configured = services.GetRequiredService<IOptions<SeedOptions>>().Value.Users;
        if (configured.Count == 0)
        {
            return;
        }

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILogger<SeedOptions>>();

        foreach (var seed in configured)
        {
            if (!Roles.All.Contains(seed.Role))
            {
                throw new InvalidOperationException(
                    $"Seed user '{seed.Email}' names unknown role '{seed.Role}'.");
            }

            if (await users.FindByEmailAsync(seed.Email) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = seed.Email,
                Email = seed.Email,
                EmailConfirmed = true,
            };

            var created = await users.CreateAsync(user, seed.Password);
            if (!created.Succeeded)
            {
                // Failing loudly at startup beats an environment that silently has no
                // administrator until someone tries to log in as one.
                throw new InvalidOperationException(
                    $"Could not seed user '{seed.Email}': " +
                    string.Join("; ", created.Errors.Select(error => error.Description)));
            }

            await users.AddToRoleAsync(user, seed.Role);
            logger.LogInformation("Seeded {Role} account {Email}.", seed.Role, seed.Email);
        }
    }
}
