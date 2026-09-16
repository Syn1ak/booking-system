using System.Security.Claims;
using BookingSystem.Api.Common;
using BookingSystem.Api.Data;
using BookingSystem.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Api.Features.Bookings;

public sealed class BookSlot : IEndpoint
{
    public sealed record Request(Guid SlotId);

    public sealed record Response(
        Guid BookingId,
        Guid SlotId,
        Guid RoomId,
        DateTime StartsAtUtc,
        DateTime EndsAtUtc,
        DateTime CreatedAtUtc);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/bookings", Handle)
           .RequireAuthorization()
           .WithName(nameof(BookSlot));

    /// <summary>
    /// Claims a slot for the caller: of several simultaneous requests for one slot, exactly one
    /// succeeds. The guarantee is the slot's concurrency tokens, not the checks below - see
    /// .claude/concurrency/concurrency.md.
    /// </summary>
    private static async Task<IResult> Handle(
        Request request,
        ClaimsPrincipal principal,
        AppDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var slot = await database.Slots
            .Where(candidate => candidate.Id == request.SlotId)
            .Where(candidate => database.Rooms.Any(room => room.Id == candidate.RoomId && room.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

        if (slot is null)
        {
            return Results.NotFound();
        }

        // 400, not 409: on this endpoint 409 means only that someone else won the race.
        if (slot.StartsAtUtc <= clock.GetUtcNow().UtcDateTime)
        {
            return Results.Problem(
                title: "Slot has already started",
                detail: "Only a slot that has not started yet can be booked.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (slot.CurrentBookingId is not null)
        {
            return await AnswerClaimedSlotAsync(database, slot, userId, cancellationToken);
        }

        var booking = new Booking
        {
            SlotId = slot.Id,
            UserId = userId,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };

        // Added first so EF assigns the key; its sequential GUIDs keep inserts off random pages.
        database.Bookings.Add(booking);
        slot.CurrentBookingId = booking.Id;

        try
        {
            // One call, so EF wraps both writes in a transaction and a lost claim discards its
            // own booking row.
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Not retried - a lost claim could only succeed if the winner cancelled. Clearing
            // the tracker discards the failed write so the read below cannot replay it.
            database.ChangeTracker.Clear();

            var claimed = await database.Slots
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == slot.Id, cancellationToken);

            return await AnswerClaimedSlotAsync(database, claimed, userId, cancellationToken);
        }

        // No Location: a booking has no single-resource route.
        return Results.Json(ToResponse(booking, slot), statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// 200 when the claim is already the caller's own - a double-click asked for nothing new -
    /// and 409 otherwise.
    /// </summary>
    private static async Task<IResult> AnswerClaimedSlotAsync(
        AppDbContext database,
        Slot slot,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var holder = await database.Bookings
            .AsNoTracking()
            .SingleOrDefaultAsync(booking => booking.Id == slot.CurrentBookingId, cancellationToken);

        // A claim naming no booking is drift, and a winner who cancelled leaves none: both 409.
        return holder is not null && holder.UserId == userId
            ? Results.Ok(ToResponse(holder, slot))
            : Results.Problem(
                title: "Slot already booked",
                detail: "Another user booked this slot first. Its schedule shows what is still free.",
                statusCode: StatusCodes.Status409Conflict);
    }

    private static Response ToResponse(Booking booking, Slot slot) =>
        new(booking.Id, slot.Id, slot.RoomId, slot.StartsAtUtc, slot.EndsAtUtc, booking.CreatedAtUtc);
}
