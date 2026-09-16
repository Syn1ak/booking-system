using BookingSystem.Api.Common;
using BookingSystem.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Api.Features.Rooms;

public sealed class GetRoom : IEndpoint
{
    public sealed record Response(
        Guid RoomId,
        string Name,
        TimeOnly OpensAtUtc,
        TimeOnly ClosesAtUtc,
        int SlotLengthMinutes);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/rooms/{roomId:guid}", Handle)
           .RequireAuthorization()
           .WithName(nameof(GetRoom));

    private static async Task<IResult> Handle(
        Guid roomId,
        AppDbContext database,
        CancellationToken cancellationToken)
    {
        // A deactivated room answers the same as one that never existed: it is gone from the
        // caller's point of view, and the row survives only so past bookings stay readable.
        var room = await database.Rooms
            .Where(room => room.Id == roomId && room.IsActive)
            .Select(room => new Response(
                room.Id,
                room.Name,
                room.OpensAtUtc,
                room.ClosesAtUtc,
                room.SlotLengthMinutes))
            .SingleOrDefaultAsync(cancellationToken);

        return room is null ? Results.NotFound() : Results.Ok(room);
    }
}
