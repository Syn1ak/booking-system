namespace BookingSystem.Api.Domain;

/// <summary>
/// One calendar row of a room's grid, and the booking that currently holds it.
/// </summary>
public sealed class Slot
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public DateTime StartsAtUtc { get; set; }

    public DateTime EndsAtUtc { get; set; }

    /// <summary>
    /// Null while the slot is free. Also a concurrency token, and deliberately not a foreign
    /// key: the claim is written before the booking row it names exists.
    /// </summary>
    public Guid? CurrentBookingId { get; set; }

    /// <summary>
    /// Set when a change to the room's rules takes this slot out of the grid. The row is kept
    /// rather than deleted because cancelled bookings still reference it as history.
    /// </summary>
    public DateTime? RetiredAtUtc { get; set; }

    /// <summary>Changes on every write, making a claim a version-checked update.</summary>
    public byte[] Version { get; set; } = [];
}
