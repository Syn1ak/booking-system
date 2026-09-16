using BookingSystem.Api.Authorization;
using BookingSystem.Api.Common;
using BookingSystem.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Api.Features.Rooms;

public sealed class DeactivateRoom : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/rooms/{roomId:guid}", Handle)
           .RequireAuthorization(Policies.CanManageRooms)
           .WithName(nameof(DeactivateRoom));

    /// <summary>
    /// DELETE is what the caller means - the room is gone from every listing - but the row,
    /// its slots and their bookings stay, so past bookings remain readable and the change is
    /// reversible.
    /// </summary>
    private static async Task<IResult> Handle(
        Guid roomId,
        AppDbContext database,
        CancellationToken cancellationToken)
    {
        // Deactivated rooms are included in the lookup, unlike every other slice: a repeated
        // DELETE should confirm the room is gone rather than fail a retry with 404.
        var room = await database.Rooms
            .SingleOrDefaultAsync(room => room.Id == roomId, cancellationToken);

        if (room is null)
        {
            return Results.NotFound();
        }

        if (room.IsActive)
        {
            room.IsActive = false;
            await database.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }
}
