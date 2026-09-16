using BookingSystem.Api.Authorization;
using BookingSystem.Api.Common;
using BookingSystem.Api.Data;
using BookingSystem.Api.Domain;
using BookingSystem.Api.RealTime;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Api.Features.Rooms;

public sealed class UpdateRoom : IEndpoint
{
    public sealed record Request(
        string Name,
        TimeOnly OpensAtUtc,
        TimeOnly ClosesAtUtc,
        int SlotLengthMinutes);

    public sealed record Response(
        Guid RoomId,
        string Name,
        TimeOnly OpensAtUtc,
        TimeOnly ClosesAtUtc,
        int SlotLengthMinutes);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Name).NotEmpty().MaximumLength(200);

            RuleFor(request => request.SlotLengthMinutes)
                .Must(Room.AllowedSlotLengthMinutes.Contains)
                .WithMessage($"Slot length must be one of: {string.Join(", ", Room.AllowedSlotLengthMinutes)}.");

            RuleFor(request => request.ClosesAtUtc)
                .GreaterThan(request => request.OpensAtUtc);

            RuleFor(request => request)
                .Must(request => Room.FitsAtLeastOneSlot(
                    request.OpensAtUtc, request.ClosesAtUtc, request.SlotLengthMinutes))
                .WithMessage("The room's day must be long enough for at least one slot.")
                .OverridePropertyName(nameof(Request.ClosesAtUtc));
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPut("/api/rooms/{roomId:guid}", Handle)
           .RequireAuthorization(Policies.CanManageRooms)
           .AddEndpointFilter<ValidationFilter<Request>>()
           .WithName(nameof(UpdateRoom));

    private static Task<IResult> Handle(
        Guid roomId,
        Request request,
        AppDbContext database,
        ScheduleNotifier notifier,
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        // The delete and the hours write share a transaction, and a retrying provider refuses a
        // transaction it did not start itself.
        database.Database
            .CreateExecutionStrategy()
            .ExecuteAsync(() => AttemptAsync(roomId, request, database, notifier, clock, cancellationToken));

    /// <summary>
    /// One attempt. A transient failure re-runs this from the top, so it re-reads the room
    /// rather than reusing anything the failed attempt left behind.
    /// </summary>
    private static async Task<IResult> AttemptAsync(
        Guid roomId,
        Request request,
        AppDbContext database,
        ScheduleNotifier notifier,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        database.ChangeTracker.Clear();

        var room = await database.Rooms
            .SingleOrDefaultAsync(room => room.Id == roomId && room.IsActive, cancellationToken);

        if (room is null)
        {
            return Results.NotFound();
        }

        var now = clock.GetUtcNow().UtcDateTime;

        // Hours may change at any time; slot length may not, while a booking stands on the
        // current grid. An hour-long booking left under a new half-hour grid overlaps two new
        // slots, and two people could then hold overlapping time through slots that are each
        // individually booked once - so refusing is the only answer that keeps the guarantee.
        if (request.SlotLengthMinutes != room.SlotLengthMinutes)
        {
            var standing = await database.Slots.CountAsync(
                slot => slot.RoomId == room.Id && slot.StartsAtUtc > now && slot.CurrentBookingId != null,
                cancellationToken);

            if (standing > 0)
            {
                return Results.Problem(
                    title: "Slot length cannot be changed while bookings stand",
                    detail: $"This room has {standing} future {(standing == 1 ? "booking" : "bookings")}. "
                            + "Slot length may change only when it has none, because a booking made on "
                            + "the current grid would overlap two slots on the new one.",
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        room.Name = request.Name;
        room.OpensAtUtc = request.OpensAtUtc;
        room.ClosesAtUtc = request.ClosesAtUtc;
        room.SlotLengthMinutes = request.SlotLengthMinutes;

        // One transaction, because the alternative orderings both fail badly: new hours with
        // the old slot rows left behind shows a grid nobody can regenerate away, and deleted
        // rows under unchanged hours is a schedule wiped by a request that reported failure.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // Future rows only, unclaimed only, and nothing is regenerated - the next read rebuilds
        // from the new rules. Slots already started, and slots somebody has booked, keep their
        // rows and keep displaying, so the schedule shows the old and new grids side by side
        // until those bookings pass or are cancelled.
        //
        // Dropping the claim check does not corrupt anything - the restricting foreign key from
        // Bookings fails the delete instead - but it turns an hours change into a 500 for any
        // room with a future booking.
        await database.Slots
            .Where(slot => slot.RoomId == room.Id
                           && slot.StartsAtUtc > now
                           && slot.CurrentBookingId == null)
            .ExecuteDeleteAsync(cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Unconditional: deleted slot rows span every date, so there is no narrower change to send.
        await notifier.ScheduleResetAsync(room.Id);

        return Results.Ok(new Response(
            room.Id, room.Name, room.OpensAtUtc, room.ClosesAtUtc, room.SlotLengthMinutes));
    }
}
