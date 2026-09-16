using BookingSystem.Api.Common;
using BookingSystem.Api.Domain;
using Microsoft.AspNetCore.Authorization;

namespace BookingSystem.Api.Authorization;

public sealed class BookingOwnerRequirement : IAuthorizationRequirement;

/// <summary>
/// Passes for a booking the caller owns, and for any booking if the caller is an admin.
/// </summary>
/// <remarks>
/// Evaluated against the loaded row rather than declared on the endpoint, because an attribute
/// runs before the row is fetched and so cannot know who owns it. Written once here rather than
/// restated at every endpoint that touches a booking: omitting such a check is a vulnerability
/// with no compile error and no failing test.
/// </remarks>
public sealed class BookingOwnerHandler : AuthorizationHandler<BookingOwnerRequirement, Booking>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BookingOwnerRequirement requirement,
        Booking booking)
    {
        if (context.User.IsInRole(Roles.Admin) || booking.UserId == context.User.UserId())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
