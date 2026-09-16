using BookingSystem.Api.Common;
using BookingSystem.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Api.Features.Rooms;

public sealed class ListRooms : IEndpoint
{
    public sealed record Response(
        Guid RoomId,
        string Name,
        TimeOnly OpensAtUtc,
        TimeOnly ClosesAtUtc,
        int SlotLengthMinutes);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/rooms", Handle)
           .RequireAuthorization()
           .WithName(nameof(ListRooms));

    private static async Task<IResult> Handle(
        AppDbContext database,
        CancellationToken cancellationToken)
    {
        var rooms = await database.Rooms
            .Where(room => room.IsActive)
            .OrderBy(room => room.Name)
            .Select(room => new Response(
                room.Id,
                room.Name,
                room.OpensAtUtc,
                room.ClosesAtUtc,
                room.SlotLengthMinutes))
            .ToListAsync(cancellationToken);

        return Results.Ok(rooms);
    }
}
