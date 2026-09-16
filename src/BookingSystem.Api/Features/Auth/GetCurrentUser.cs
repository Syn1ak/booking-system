using System.Security.Claims;
using BookingSystem.Api.Common;

namespace BookingSystem.Api.Features.Auth;

public sealed class GetCurrentUser : IEndpoint
{
    public sealed record Response(Guid UserId, string? Email, string[] Roles);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/auth/me", Handle)
           .RequireAuthorization()
           .WithName(nameof(GetCurrentUser));

    private static IResult Handle(ClaimsPrincipal user) =>
        Results.Ok(new Response(
            user.UserId(),
            user.FindFirstValue(ClaimTypes.Email),
            [.. user.FindAll(ClaimTypes.Role).Select(claim => claim.Value)]));
}
