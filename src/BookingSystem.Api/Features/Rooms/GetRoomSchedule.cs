using System.Security.Claims;
using BookingSystem.Api.Common;
using BookingSystem.Api.Data;
using BookingSystem.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Api.Features.Rooms;

public sealed class GetRoomSchedule : IEndpoint
{
    public sealed record Response(Guid RoomId, DateOnly Date, SlotResponse[] Slots);

    /// <summary>
    /// <paramref name="MyBookingId"/> is set only when the caller holds the slot, which also
    /// answers whether the booking is theirs. Whose booking it is otherwise is deliberately
    /// absent: a regular user may not see other users' bookings, and this is the endpoint where
    /// that would leak by accident.
    /// </summary>
    public sealed record SlotResponse(
        Guid SlotId,
        DateTime StartsAtUtc,
        DateTime EndsAtUtc,
        bool IsBooked,
        Guid? MyBookingId);

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
        ClaimsPrincipal principal,
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

        var own = await ReadOwnClaimsAsync(database, slots, principal.UserId(), cancellationToken);

        return Results.Ok(new Response(
            room.Id,
            requested,
            [.. slots.Select(slot => new SlotResponse(
                slot.Id,
                slot.StartsAtUtc,
                slot.EndsAtUtc,
                slot.CurrentBookingId is not null,
                slot.CurrentBookingId is { } claim && own.Contains(claim) ? claim : null))]));
    }

    /// <summary>
    /// Which of these slots the caller holds. Restricted to the caller inside the query rather
    /// than after it, so no other user's booking is ever loaded to be filtered out.
    /// </summary>
    private static async Task<HashSet<Guid>> ReadOwnClaimsAsync(
        AppDbContext database,
        IReadOnlyList<Slot> slots,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var claims = slots
            .Where(slot => slot.CurrentBookingId is not null)
            .Select(slot => slot.CurrentBookingId!.Value)
            .ToList();

        if (claims.Count == 0)
        {
            return [];
        }

        return [.. await database.Bookings
            .Where(booking => claims.Contains(booking.Id) && booking.UserId == userId)
            .Select(booking => booking.Id)
            .ToListAsync(cancellationToken)];
    }
}
