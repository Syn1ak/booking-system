namespace BookingSystem.Api.Domain;

/// <summary>
/// A bookable resource. The room owns the <em>rules</em> of its slot grid — when the day
/// opens, when it closes, and how long one slot is — while the rows those rules produce live
/// in <see cref="Slot"/>.
/// </summary>
public sealed class Room
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// First slot of the day starts here. A UTC wall clock: the system has no per-room time
    /// zones, so this is a plain time of day interpreted in UTC.
    /// </summary>
    public TimeOnly OpensAtUtc { get; set; }

    /// <summary>
    /// No slot may end after this. A grid that does not divide evenly drops the trailing
    /// remainder rather than overrunning.
    /// </summary>
    public TimeOnly ClosesAtUtc { get; set; }

    public int SlotLengthMinutes { get; set; }

    /// <summary>
    /// Cleared instead of deleting the row. A hard delete would cascade through slots into
    /// bookings and destroy several people's meetings with no record they existed.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
