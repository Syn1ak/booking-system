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

    /// <summary>Null while the slot is free. Also a concurrency token.</summary>
    public Guid? CurrentBookingId { get; set; }

    /// <summary>Changes on every write, making a claim a version-checked update.</summary>
    public byte[] Version { get; set; } = [];
}
