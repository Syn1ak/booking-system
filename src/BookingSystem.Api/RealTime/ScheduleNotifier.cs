using Microsoft.AspNetCore.SignalR;

namespace BookingSystem.Api.RealTime;

/// <summary>
/// Tells a room's watchers about a committed change. Call only after the commit: an announced
/// write cannot be taken back if it rolls back.
/// </summary>
public sealed class ScheduleNotifier(
    IHubContext<ScheduleHub, IScheduleClient> hub,
    ILogger<ScheduleNotifier> logger)
{
    public Task SlotChangedAsync(SlotChange change) =>
        SendAsync(change.RoomId, nameof(IScheduleClient.SlotChanged), client => client.SlotChanged(change));

    public Task ScheduleResetAsync(Guid roomId) =>
        SendAsync(roomId, nameof(IScheduleClient.ScheduleReset), client => client.ScheduleReset(roomId));

    private async Task SendAsync(Guid roomId, string eventName, Func<IScheduleClient, Task> send)
    {
        try
        {
            await send(hub.Clients.Group(ScheduleHub.GroupFor(roomId)));
        }
        catch (Exception exception)
        {
            // Must not throw: callers run inside an execution strategy, which would retry a
            // write that has already committed.
            logger.LogError(exception, "Could not publish {Event} to watchers of room {RoomId}", eventName, roomId);
        }
    }
}
