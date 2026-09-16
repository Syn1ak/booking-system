using BookingSystem.Api.Authorization;
using BookingSystem.Api.Common;
using BookingSystem.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Api.Features.Bookings;

public sealed class ListAllBookings : IEndpoint
{
    public sealed record Response(
        Guid BookingId,
        Guid SlotId,
        Guid RoomId,
        string RoomName,
        DateTime StartsAtUtc,
        DateTime EndsAtUtc,
        Guid UserId,
        string? UserEmail,
        DateTime CreatedAtUtc,
        DateTime? CancelledAtUtc);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/api/admin/bookings", Handle)
           .RequireAuthorization(Policies.CanViewAllBookings)
           .WithName(nameof(ListAllBookings));

    /// <summary>
    /// Every user's bookings, naming who holds each one - which is the point of the endpoint and
    /// the reason it is admin-only. Cancelled bookings are included unless excluded, since the
    /// admin view of history is why cancelling keeps the row. Not paged, at this data size.
    /// </summary>
    private static async Task<IResult> Handle(
        Guid? roomId,
        DateOnly? from,
        DateOnly? to,
        bool? includeCancelled,
        AppDbContext database,
        CancellationToken cancellationToken)
    {
        var fromUtc = from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // `to` names a day the caller expects to see, so the bound is the start of the next one.
        var toUtc = to?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var showCancelled = includeCancelled ?? true;

        var bookings = await (
            from booking in database.Bookings
            join slot in database.Slots on booking.SlotId equals slot.Id
            join room in database.Rooms on slot.RoomId equals room.Id
            join user in database.Users on booking.UserId equals user.Id
            where (roomId == null || room.Id == roomId)
                  && (fromUtc == null || slot.StartsAtUtc >= fromUtc)
                  && (toUtc == null || slot.StartsAtUtc < toUtc)
                  && (showCancelled || booking.CancelledAtUtc == null)
            orderby slot.StartsAtUtc descending
            select new Response(
                booking.Id,
                slot.Id,
                room.Id,
                room.Name,
                slot.StartsAtUtc,
                slot.EndsAtUtc,
                user.Id,
                user.Email,
                booking.CreatedAtUtc,
                booking.CancelledAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(bookings);
    }
}
