using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookingSystem.Api.RealTime;
using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Features.RealTime;

/// <remarks>
/// The hub runs self-hosted here. These tests prove the contract and the publish points, not
/// Azure SignalR Service - a green run says nothing about the deployment's connection string.
/// </remarks>
[Collection(ApiCollection.Name)]
public sealed class ScheduleHubTests(ApiFactory factory)
{
    [Fact]
    public async Task Connect_WithoutAToken_IsRefused()
    {
        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => HubClient.ConnectAsync(factory, accessToken: null));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }

    [Fact]
    public async Task Book_WhileWatching_PublishesTheSlotAsBookedWithoutItsHolder()
    {
        var room = await factory.CreateRoomAsync();
        await using var watcher = await WatchAsync(room.Id);
        var booker = await factory.CreateUserClientAsync();

        var booking = await booker.BookFirstFreeSlotAsync(room.Id, BookingData.Tomorrow);
        var payload = await watcher.NextSlotChangeAsync();

        // The whole payload, so a field naming the holder cannot be added without failing here.
        Assert.Equal(
            ["isBooked", "roomId", "sequence", "slotId", "startsAtUtc"],
            payload.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));

        var change = ToSlotChange(payload);
        Assert.Equal(new SlotChange(room.Id, booking.SlotId, booking.StartsAtUtc, true, booking.Sequence), change);
    }

    [Fact]
    public async Task Cancel_WhileWatching_PublishesTheSlotAsFreeWithAGreaterSequence()
    {
        var room = await factory.CreateRoomAsync();
        await using var watcher = await WatchAsync(room.Id);
        var booker = await factory.CreateUserClientAsync();
        var booking = await booker.BookFirstFreeSlotAsync(room.Id, BookingData.Tomorrow);
        var booked = ToSlotChange(await watcher.NextSlotChangeAsync());

        (await booker.CancelAsync(booking.BookingId)).EnsureSuccessStatusCode();
        var freed = ToSlotChange(await watcher.NextSlotChangeAsync());

        Assert.Equal(booking.SlotId, freed.SlotId);
        Assert.False(freed.IsBooked);
        Assert.True(freed.Sequence > booked.Sequence);
    }

    [Fact]
    public async Task Book_InAnotherRoom_IsNotPublishedToThisRoomsWatchers()
    {
        var watched = await factory.CreateRoomAsync();
        var other = await factory.CreateRoomAsync();
        await using var watcher = await WatchAsync(watched.Id);
        var booker = await factory.CreateUserClientAsync();

        await booker.BookFirstFreeSlotAsync(other.Id, BookingData.Tomorrow);
        await booker.BookFirstFreeSlotAsync(watched.Id, BookingData.Tomorrow);

        // Both publishes reach one connection in order, so the watched room's event arriving
        // first proves the other room's was never sent - without waiting out a timeout.
        Assert.Equal(watched.Id, ToSlotChange(await watcher.NextSlotChangeAsync()).RoomId);
    }

    [Fact]
    public async Task UpdateRoom_WhileWatching_PublishesAScheduleReset()
    {
        var room = await factory.CreateRoomAsync();
        await using var watcher = await WatchAsync(room.Id);
        var admin = await factory.CreateAdminClientAsync();

        (await admin.PutAsJsonAsync($"/api/rooms/{room.Id}", new
        {
            name = room.Name,
            opensAtUtc = "10:00:00",
            closesAtUtc = "14:00:00",
            slotLengthMinutes = room.SlotLengthMinutes,
        })).EnsureSuccessStatusCode();

        Assert.Equal(room.Id, await watcher.NextScheduleResetAsync());
    }

    [Fact]
    public async Task DeactivateRoom_WhileWatching_PublishesAScheduleReset()
    {
        var room = await factory.CreateRoomAsync();
        await using var watcher = await WatchAsync(room.Id);
        var admin = await factory.CreateAdminClientAsync();

        (await admin.DeleteAsync($"/api/rooms/{room.Id}")).EnsureSuccessStatusCode();

        Assert.Equal(room.Id, await watcher.NextScheduleResetAsync());
    }

    private async Task<HubClient> WatchAsync(Guid roomId)
    {
        var token = await factory.CreateClient().RegisterAndGetTokenAsync(TestData.NewEmail());
        var watcher = await HubClient.ConnectAsync(factory, token);
        await watcher.WatchAsync(roomId);

        return watcher;
    }

    private static SlotChange ToSlotChange(JsonElement payload) =>
        payload.Deserialize<SlotChange>(JsonSerializerOptions.Web)!;
}
