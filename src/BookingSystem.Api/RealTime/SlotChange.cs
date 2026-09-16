namespace BookingSystem.Api.RealTime;

/// <param name="Sequence">
/// The slot row's version after the change. Only ever compared, never interpreted: a client
/// ignores an event no newer than what it holds, and recognises its own booking by the
/// sequence its booking response returned.
/// </param>
/// <remarks>
/// Deliberately says nothing about who holds the slot. One payload reaches every viewer of
/// the room, so adding that field would leak what the schedule endpoint tells each caller
/// only about themselves.
/// </remarks>
public sealed record SlotChange(
    Guid RoomId,
    Guid SlotId,
    DateTime StartsAtUtc,
    bool IsBooked,
    long Sequence);
