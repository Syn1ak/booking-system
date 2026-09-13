using BookingSystem.Api.Common;

namespace BookingSystem.Api.Features.Health;

public sealed class GetHealth : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))
           .WithName(nameof(GetHealth));
}
