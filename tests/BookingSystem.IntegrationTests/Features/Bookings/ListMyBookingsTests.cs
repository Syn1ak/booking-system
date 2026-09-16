using System.Net;
using System.Net.Http.Json;
using BookingSystem.Api.Features.Bookings;
using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Features.Bookings;

[Collection(ApiCollection.Name)]
public sealed class ListMyBookingsTests(ApiFactory factory)
{
    [Fact]
    public async Task List_WithoutAToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOnlyTheCallersOwnBookings()
    {
        var room = await factory.CreateRoomAsync();
        var mine = await factory.CreateUserClientAsync();
        var theirs = await factory.CreateUserClientAsync();
        var schedule = await mine.ReadScheduleAsync(room.Id, BookingData.Tomorrow);

        var ours = await mine.BookSucceedsAsync(schedule.Slots[0].SlotId);
        await theirs.BookSucceedsAsync(schedule.Slots[1].SlotId);

        var listed = await ListAsync(mine);

        Assert.Equal([ours.BookingId], listed.Select(booking => booking.BookingId));
    }

    [Fact]
    public async Task List_CarriesTheRoomAndSlotOfEachBooking()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();
        var booking = await client.BookFirstFreeSlotAsync(room.Id, BookingData.Tomorrow);

        var listed = Assert.Single(await ListAsync(client));

        Assert.Equal(room.Id, listed.RoomId);
        Assert.Equal(room.Name, listed.RoomName);
        Assert.Equal(booking.SlotId, listed.SlotId);
        Assert.Equal(booking.StartsAtUtc, listed.StartsAtUtc);
        Assert.Null(listed.CancelledAtUtc);
    }

    [Fact]
    public async Task List_IncludesCancelledBookings()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();
        var booking = await client.BookFirstFreeSlotAsync(room.Id, BookingData.Tomorrow);
        (await client.CancelAsync(booking.BookingId)).EnsureSuccessStatusCode();

        var listed = Assert.Single(await ListAsync(client));

        // Hiding them would make a cancellation look like data loss.
        Assert.Equal(booking.BookingId, listed.BookingId);
        Assert.NotNull(listed.CancelledAtUtc);
    }

    [Fact]
    public async Task List_OrdersTheLatestSlotFirst()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();
        var schedule = await client.ReadScheduleAsync(room.Id, BookingData.Tomorrow);

        var earlier = await client.BookSucceedsAsync(schedule.Slots[0].SlotId);
        var later = await client.BookSucceedsAsync(schedule.Slots[1].SlotId);

        var listed = await ListAsync(client);

        Assert.Equal([later.BookingId, earlier.BookingId], listed.Select(booking => booking.BookingId));
    }

    private static async Task<ListMyBookings.Response[]> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<ListMyBookings.Response[]>("/api/bookings"))!;
}
