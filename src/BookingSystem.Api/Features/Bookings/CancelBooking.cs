using System.Security.Claims;
using BookingSystem.Api.Authorization;
using BookingSystem.Api.Common;
using BookingSystem.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Api.Features.Bookings;

public sealed class CancelBooking : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapDelete("/api/bookings/{bookingId:guid}", Handle)
           .RequireAuthorization()
           .WithName(nameof(CancelBooking));

    /// <summary>
    /// Cancels a booking and frees its slot. DELETE is what the caller means - the booking is
    /// gone from their point of view - while the row itself is kept so an admin can still see
    /// it and so the slot can be booked again.
    /// </summary>
    private static Task<IResult> Handle(
        Guid bookingId,
        ClaimsPrincipal principal,
        AppDbContext database,
        IAuthorizationService authorization,
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        database.Database
            .CreateExecutionStrategy()
            .ExecuteAsync(() => AttemptAsync(
                bookingId, principal, database, authorization, clock, cancellationToken));

    private static async Task<IResult> AttemptAsync(
        Guid bookingId,
        ClaimsPrincipal principal,
        AppDbContext database,
        IAuthorizationService authorization,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        database.ChangeTracker.Clear();

        var booking = await database.Bookings
            .SingleOrDefaultAsync(candidate => candidate.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return Results.NotFound();
        }

        var permitted = await authorization.AuthorizeAsync(principal, booking, Policies.CanCancelBooking);

        if (!permitted.Succeeded)
        {
            return Results.Forbid();
        }

        // Idempotent: the caller asked for the booking not to stand, and it does not stand.
        if (booking.CancelledAtUtc is not null)
        {
            return Results.NoContent();
        }

        var startsAtUtc = await database.Slots
            .Where(slot => slot.Id == booking.SlotId)
            .Select(slot => slot.StartsAtUtc)
            .SingleAsync(cancellationToken);

        if (startsAtUtc <= clock.GetUtcNow().UtcDateTime)
        {
            return Results.Problem(
                title: "Slot has already started",
                detail: "A booking can only be cancelled before its slot starts.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // One transaction: a released slot whose booking still stands is unbookable, because the
        // backstop index still counts that booking as live; a cancelled booking whose slot is
        // still claimed leaves the slot unbookable forever.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        booking.CancelledAtUtc = clock.GetUtcNow().UtcDateTime;

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another cancellation of this booking committed first, which is what this caller
            // asked for too.
            await transaction.RollbackAsync(cancellationToken);

            return Results.NoContent();
        }

        // Released only if the slot still holds *this* booking, so that releasing can never
        // clear somebody else's claim. The checks above happen to rule that out today; this
        // predicate is what makes the write correct on its own, the way the claim's tokens are
        // what make booking correct rather than the checks that precede them.
        await database.Slots
            .Where(slot => slot.Id == booking.SlotId && slot.CurrentBookingId == booking.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(slot => slot.CurrentBookingId, (Guid?)null),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Results.NoContent();
    }
}
