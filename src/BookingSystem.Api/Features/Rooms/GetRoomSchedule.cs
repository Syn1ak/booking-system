using BookingSystem.Api.Common;
using BookingSystem.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Api.Features.Rooms;

public sealed class GetRoomSchedule : IEndpoint
{
    public sealed record Response(Guid RoomId, DateOnly Date, SlotResponse[] Slots);

    public sealed record SlotResponse(Guid SlotId, DateTime StartsAtUtc, DateTime EndsAtUtc);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/rooms/{roomId:guid}/schedule", Handle)
           .RequireAuthorization()
           .WithName(nameof(GetRoomSchedule));

    /// <summary>
    /// Reads a room's schedule for a date, defaulting to today. This GET writes: a date read
    /// for the first time materialises its slot rows. The write is idempotent, bounded to one
    /// room and one date, and happens only once per date - see the scheduling decision record.
    /// </summary>
    private static async Task<IResult> Handle(
        Guid roomId,
        DateOnly? date,
        AppDbContext database,
        SlotGenerator generator,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var room = await database.Rooms
            .AsNoTracking()
            .SingleOrDefaultAsync(room => room.Id == roomId && room.IsActive, cancellationToken);

        if (room is null)
        {
            return Results.NotFound();
        }

        var requested = date ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var slots = await generator.EnsureSlotsAsync(room, requested, cancellationToken);

        // Null is a date the system will not open for booking, which is not the same answer as
        // a room with no slots that day - so it is refused rather than returned empty.
        if (slots is null)
        {
            return Results.Problem(
                title: "Date outside the booking window",
                detail: $"Schedules can be read from today through {generator.BookingWindowDays} days ahead.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(new Response(
            room.Id,
            requested,
            [.. slots.Select(slot => new SlotResponse(slot.Id, slot.StartsAtUtc, slot.EndsAtUtc))]));
    }
}
