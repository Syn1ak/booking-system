using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BookingSystem.Api.RealTime;

/// <summary>
/// Carries schedule changes to browsers watching a room - see
/// .claude/realtime/realtime.md. Read-only: nothing arriving here is ever written.
/// </summary>
[Authorize]
public sealed class ScheduleHub : Hub<IScheduleClient>
{
    public const string Path = "/hub/schedule";

    /// <remarks>
    /// Authentication is the only check by design. These messages carry less than the schedule
    /// endpoint already returns to any authenticated user, so privacy rests on what the payload
    /// omits rather than on who may join a group.
    /// </remarks>
    public Task Watch(Guid roomId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(roomId), Context.ConnectionAborted);

    public Task Unwatch(Guid roomId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(roomId), Context.ConnectionAborted);

    public static string GroupFor(Guid roomId) => $"room:{roomId}";
}
