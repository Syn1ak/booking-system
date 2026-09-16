namespace BookingSystem.Api.Domain;

/// <summary>
/// One calendar row of a room's grid: which room, when it starts, when it ends.
/// </summary>
public sealed class Slot
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public DateTime StartsAtUtc { get; set; }

    public DateTime EndsAtUtc { get; set; }
}
