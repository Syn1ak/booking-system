namespace BookingSystem.Api.Domain;

/// <summary>
/// A user's claim on one slot. The claim itself is held by
/// <see cref="Slot.CurrentBookingId"/>; this row records who made it and when, and outlives
/// its cancellation.
/// </summary>
public sealed class Booking
{
    public Guid Id { get; set; }

    public Guid SlotId { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// When the booking was cancelled, or null while it stands. Cancelling sets this instead of
    /// deleting the row: the admin view of every user's bookings has to include the cancelled
    /// ones, and the index backstopping the booking guarantee filters on this column.
    /// </summary>
    public DateTime? CancelledAtUtc { get; set; }

    /// <summary>
    /// Stops two simultaneous cancellations of this booking from both applying.
    /// </summary>
    public byte[] Version { get; set; } = [];
}
