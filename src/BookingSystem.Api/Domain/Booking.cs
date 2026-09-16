namespace BookingSystem.Api.Domain;

/// <summary>
/// A user's claim on one slot, kept after cancellation.
/// <see cref="Slot.CurrentBookingId"/> holds the claim itself.
/// </summary>
public sealed class Booking
{
    public Guid Id { get; set; }

    public Guid SlotId { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Null while the booking stands. Set instead of deleting the row: admins read cancelled
    /// bookings, and the backstop index filters on it.
    /// </summary>
    public DateTime? CancelledAtUtc { get; set; }

    /// <summary>Stops two simultaneous cancellations from both applying.</summary>
    public byte[] Version { get; set; } = [];
}
