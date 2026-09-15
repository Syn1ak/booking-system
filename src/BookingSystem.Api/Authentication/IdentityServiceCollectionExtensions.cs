using BookingSystem.Api.Data;
using BookingSystem.Api.Domain;
using Microsoft.AspNetCore.Identity;

namespace BookingSystem.Api.Authentication;

public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationIdentity(this IServiceCollection services)
    {
        // AddIdentityCore rather than AddIdentity: the latter registers cookie authentication
        // schemes and makes one of them the default, which would silently displace the JWT
        // bearer scheme configured alongside it.
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager();

        return services;
    }
}
