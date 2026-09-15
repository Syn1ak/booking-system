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
        });

        return services;
    }
}
