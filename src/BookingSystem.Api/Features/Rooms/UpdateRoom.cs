using BookingSystem.Api.Authorization;
using BookingSystem.Api.Common;
using BookingSystem.Api.Data;
using BookingSystem.Api.Domain;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Api.Features.Rooms;

public sealed class UpdateRoom : IEndpoint
{
    /// <remarks>
    /// Slot length is deliberately absent. Changing it is allowed only while the room has no
    /// future bookings, and that check queries a table that does not exist yet - so the field
    /// and its conflict response ship with the bookings feature rather than unguarded here.
    /// </remarks>
    public sealed record Request(string Name, TimeOnly OpensAtUtc, TimeOnly ClosesAtUtc);

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

            RuleFor(request => request.ClosesAtUtc)
                .GreaterThan(request => request.OpensAtUtc);
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
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        // The delete and the hours write share a transaction, and a retrying provider refuses a
        // transaction it did not start itself.
        database.Database
            .CreateExecutionStrategy()
            .ExecuteAsync(() => AttemptAsync(roomId, request, database, clock, cancellationToken));

    /// <summary>
    /// One attempt. A transient failure re-runs this from the top, so it re-reads the room
    /// rather than reusing anything the failed attempt left behind.
    /// </summary>
    private static async Task<IResult> AttemptAsync(
        Guid roomId,
        Request request,
        AppDbContext database,
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

        // Checked here rather than in the validator, which cannot see the room's slot length.
        if (!Room.FitsAtLeastOneSlot(request.OpensAtUtc, request.ClosesAtUtc, room.SlotLengthMinutes))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(Request.ClosesAtUtc)] =
                    [$"The room's day must be long enough for one {room.SlotLengthMinutes} minute slot."],
            });
        }

        room.Name = request.Name;
        room.OpensAtUtc = request.OpensAtUtc;
        room.ClosesAtUtc = request.ClosesAtUtc;

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
        var now = clock.GetUtcNow().UtcDateTime;
        await database.Slots
            .Where(slot => slot.RoomId == room.Id
                           && slot.StartsAtUtc > now
                           && slot.CurrentBookingId == null)
            .ExecuteDeleteAsync(cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new Response(
            room.Id, room.Name, room.OpensAtUtc, room.ClosesAtUtc, room.SlotLengthMinutes));
    }
}
