namespace BookingSystem.Api.Domain;

/// <summary>
/// One calendar row of a room's grid: which room, when it starts, when it ends.
/// </summary>
/// <remarks>
/// Deliberately inert — there is no "is booked" flag. A slot is booked precisely when a live
/// booking row references it, and the schedule projection asks that question directly. A flag
/// would store one real-world fact in two places, and a drift between them would show the
/// wrong availability to every viewer with nothing detecting it.
/// </remarks>
public sealed class Slot
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public DateTime StartsAtUtc { get; set; }

    public DateTime EndsAtUtc { get; set; }
}
