namespace BookingSystem.Api.RealTime;

public interface IScheduleClient
{
    Task SlotChanged(SlotChange change);

    /// <summary>Everything known about this room is stale - fetch its schedule again.</summary>
    Task ScheduleReset(Guid roomId);
}
