namespace BookingSystem.Api.Domain;

/// <summary>
/// One calendar row of a room's grid - which room, when it starts, when it ends - and the
/// booking that currently holds it. Claiming that column is how a booking request wins;
/// see .claude/concurrency/concurrency.md.
/// </summary>
public sealed class Slot
{
    public Guid Id { get; set; }

    public Guid RoomId { get; set; }

    public DateTime StartsAtUtc { get; set; }

    public DateTime EndsAtUtc { get; set; }

    /// <summary>
    /// The booking holding this slot, or null while it is free. Also a concurrency token, so a
    /// claim can only be written over a slot that was free when it was read.
    /// </summary>
    public Guid? CurrentBookingId { get; set; }

    /// <summary>
    /// Changes on every write to the row, making a claim a version-checked update: simultaneous
    /// requests all read the same value, so exactly one update finds its row.
    /// </summary>
    public byte[] Version { get; set; } = [];
}
