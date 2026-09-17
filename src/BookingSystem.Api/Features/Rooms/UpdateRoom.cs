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
        // The refusal itself is below, after the retire, and that placement is the guarantee.
        var slotLengthChanged = request.SlotLengthMinutes != room.SlotLengthMinutes;

        room.Name = request.Name;
        room.OpensAtUtc = request.OpensAtUtc;
        room.ClosesAtUtc = request.ClosesAtUtc;
        room.SlotLengthMinutes = request.SlotLengthMinutes;

        // One transaction, because the alternative orderings both fail badly: new hours with
        // the old slot rows left behind shows a grid nobody can regenerate away, and deleted
        // rows under unchanged hours is a schedule wiped by a request that reported failure.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // The room row is written first so its exclusive lock is held for the rest of the
        // transaction. A schedule read that has not reached the room yet blocks here instead of
        // materialising a grid from the rules this request is replacing.
        await database.SaveChangesAsync(cancellationToken);

        // Future rows only, unclaimed only, and nothing is regenerated - the next read rebuilds
        // from the new rules. Slots already started, and slots somebody has booked, keep their
        // rows and keep displaying, so the schedule shows the old and new grids side by side
        // until those bookings pass or are cancelled.
        //
        // Retired, not deleted: a free slot may still be referenced by cancelled bookings, whose
        // history the restricting foreign key rightly refuses to orphan. Retiring also moves the
        // rowversion, so a claim racing this change fails its token instead of landing on a row
        // that has left the grid.
        await database.Slots
            .Where(slot => slot.RoomId == room.Id
                           && slot.RetiredAtUtc == null
                           && slot.StartsAtUtc > now
                           && slot.CurrentBookingId == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(slot => slot.RetiredAtUtc, now),
                cancellationToken);

        // Counted after the retire and inside its transaction, and that ordering is the whole
        // of the guarantee rather than a tidier arrangement of the same two statements.
        //
        // Counting first and writing afterwards is the check-then-write that concurrency.md
        // disqualifies for booking, and it fails here for the same reason: a claim committing
        // between the count and the retire is invisible to the count, and the retire then
        // spares the very slot it claimed because that slot is no longer free. Both requests
        // report success, and two users hold overlapping time through slots each booked once -
        // the failure this restriction exists to prevent, reached by the one path that did not
        // go through the booking mechanism. It reproduced in roughly one race in fourteen.
        //
        // After the retire there is no such gap, because the retire's own exclusive locks are
        // the serialisation point. A claim on a future free slot either landed before the retire
        // read that row - so the row is still active and claimed, and is counted here - or it
        // waits on the lock until this transaction ends and then fails its concurrency token
        // against the retired row. Exactly one of the two requests can succeed.
        if (slotLengthChanged)
        {
            var standing = await database.Slots.CountAsync(
                slot => slot.RoomId == room.Id
                        && slot.RetiredAtUtc == null
                        && slot.StartsAtUtc > now
                        && slot.CurrentBookingId != null,
                cancellationToken);

            if (standing > 0)
            {
                // Rolling back leaves the room's rules and every slot row exactly as they were,
                // so a refused change retires nothing.
                await transaction.RollbackAsync(cancellationToken);

                return Results.Problem(
                    title: "Slot length cannot be changed while bookings stand",
                    detail: $"This room has {standing} future {(standing == 1 ? "booking" : "bookings")}. "
                            + "Slot length may change only when it has none, because a booking made on "
                            + "the current grid would overlap two slots on the new one.",
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        await transaction.CommitAsync(cancellationToken);

        // Unconditional: deleted slot rows span every date, so there is no narrower change to send.
        await notifier.ScheduleResetAsync(room.Id);

        return Results.Ok(new Response(
            room.Id, room.Name, room.OpensAtUtc, room.ClosesAtUtc, room.SlotLengthMinutes));
    }
}
