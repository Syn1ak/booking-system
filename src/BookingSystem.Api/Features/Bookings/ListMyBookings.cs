using System.Security.Claims;
using BookingSystem.Api.Common;
using BookingSystem.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Api.Features.Bookings;

public sealed class ListMyBookings : IEndpoint
{
    public sealed record Response(
        Guid BookingId,
        Guid SlotId,
        Guid RoomId,
        string RoomName,
        DateTime StartsAtUtc,
        DateTime EndsAtUtc,
        DateTime CreatedAtUtc,
        DateTime? CancelledAtUtc);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/bookings", Handle)
           .RequireAuthorization()
           .WithName(nameof(ListMyBookings));

    /// <summary>
    /// The caller's own bookings, cancelled ones included, latest slot first.
    /// </summary>
    /// <remarks>
    /// Restricted to the caller inside the query rather than by filtering what came back:
    /// loading everyone's bookings to discard most of them would be slower and one edit away
    /// from a leak. No active-room filter either - a booking in a room since deactivated is
    /// still the caller's booking and still happened.
    /// </remarks>
    private static async Task<IResult> Handle(
        ClaimsPrincipal principal,
        AppDbContext database,
        CancellationToken cancellationToken)
    {
        var userId = principal.UserId();

        var bookings = await (
            from booking in database.Bookings
            join slot in database.Slots on booking.SlotId equals slot.Id
            join room in database.Rooms on slot.RoomId equals room.Id
            where booking.UserId == userId
            orderby slot.StartsAtUtc descending
            select new Response(
                booking.Id,
                slot.Id,
                room.Id,
                room.Name,
                slot.StartsAtUtc,
                slot.EndsAtUtc,
                booking.CreatedAtUtc,
                booking.CancelledAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(bookings);
    }
}
