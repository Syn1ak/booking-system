using BookingSystem.Api.Authorization;
using BookingSystem.Api.Common;
using BookingSystem.Api.Data;
using BookingSystem.Api.Domain;
using FluentValidation;

namespace BookingSystem.Api.Features.Rooms;

public sealed class CreateRoom : IEndpoint
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

            // A day too short for one slot would generate an empty grid on every read, leaving
            // a room that exists, lists, and can never be booked.
            RuleFor(request => request)
                .Must(request => request.ClosesAtUtc - request.OpensAtUtc
                                 >= TimeSpan.FromMinutes(request.SlotLengthMinutes))
                .WithMessage("The room's day must be long enough for at least one slot.")
                .OverridePropertyName(nameof(Request.ClosesAtUtc));
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/rooms", Handle)
           .RequireAuthorization(Policies.CanManageRooms)
           .AddEndpointFilter<ValidationFilter<Request>>()
           .WithName(nameof(CreateRoom));

    private static async Task<IResult> Handle(
        Request request,
        AppDbContext database,
        CancellationToken cancellationToken)
    {
        var room = new Room
        {
            Name = request.Name,
            OpensAtUtc = request.OpensAtUtc,
            ClosesAtUtc = request.ClosesAtUtc,
            SlotLengthMinutes = request.SlotLengthMinutes,
        };

        database.Rooms.Add(room);
        await database.SaveChangesAsync(cancellationToken);

        // No slots are generated here: they materialise when a date is read, so a room created
        // today is bookable on any date in the window without a second write path.
        return Results.Created(
            $"/api/rooms/{room.Id}",
            new Response(room.Id, room.Name, room.OpensAtUtc, room.ClosesAtUtc, room.SlotLengthMinutes));
    }
}
