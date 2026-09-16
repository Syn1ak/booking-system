using Microsoft.AspNetCore.Authorization;

namespace BookingSystem.Api.Authorization;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                Policies.CanManageRooms,
                policy => policy.RequireRole(Roles.Admin));

            options.AddPolicy(
                Policies.CanViewAllBookings,
                policy => policy.RequireRole(Roles.Admin));

            options.AddPolicy(
                Policies.CanCancelBooking,
                policy => policy.RequireAuthenticatedUser().AddRequirements(new BookingOwnerRequirement()));
        });

        // The requirement above is satisfied by a resource the endpoint has already loaded, so
        // its handler has to be resolvable rather than named on an attribute.
        services.AddSingleton<IAuthorizationHandler, BookingOwnerHandler>();

        return services;
    }
}
