namespace BookingSystem.Api.Domain;

/// <summary>
/// A bookable resource. The room owns the <em>rules</em> of its slot grid — when the day
/// opens, when it closes, and how long one slot is — while the rows those rules produce live
/// in <see cref="Slot"/>.
/// </summary>
public sealed class Room
{
    /// <summary>
    /// Slot lengths a room may use. A fixed set rather than any divisor, so the dropped
    /// remainder of a day stays something a schedule can explain.
    /// </summary>
    public static readonly int[] AllowedSlotLengthMinutes = [15, 30, 60];

    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>A time of day in UTC: the system has no per-room time zones.</summary>
    public TimeOnly OpensAtUtc { get; set; }

    /// <summary>No slot may end after this.</summary>
    public TimeOnly ClosesAtUtc { get; set; }

    public int SlotLengthMinutes { get; set; }

    /// <summary>
    /// Cleared instead of deleting the row: a hard delete would take the room's bookings with
    /// it, destroying meetings with no record they existed.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether a day of these hours fits at least one slot of this length. A room that fits
    /// none generates an empty grid on every read and can never be booked.
    /// </summary>
    public static bool FitsAtLeastOneSlot(TimeOnly opensAtUtc, TimeOnly closesAtUtc, int slotLengthMinutes) =>
        closesAtUtc - opensAtUtc >= TimeSpan.FromMinutes(slotLengthMinutes);

    /// <summary>
    /// The slot boundaries this room's rules produce on <paramref name="dateUtc"/>, in order.
    /// The single definition of the grid: slot rows are materialised from this and nothing
    /// else computes a boundary. A day that does not divide evenly drops the remainder.
    /// </summary>
    public IEnumerable<(DateTime StartsAtUtc, DateTime EndsAtUtc)> SlotsOn(DateOnly dateUtc)
    {
        // A non-positive length would make the loop below non-terminating.
        if (SlotLengthMinutes <= 0)
        {
            throw new InvalidOperationException(
                $"Room {Id} has a slot length of {SlotLengthMinutes} minutes, which produces no grid.");
        }

        var length = TimeSpan.FromMinutes(SlotLengthMinutes);
        var closes = dateUtc.ToDateTime(ClosesAtUtc, DateTimeKind.Utc);

        for (var start = dateUtc.ToDateTime(OpensAtUtc, DateTimeKind.Utc);
             start + length <= closes;
             start += length)
        {
            yield return (start, start + length);
        }
    }
}
