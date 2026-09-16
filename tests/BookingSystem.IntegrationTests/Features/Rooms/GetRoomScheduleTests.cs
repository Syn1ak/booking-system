using System.Net;
using System.Net.Http.Json;
using BookingSystem.Api.Domain;
using BookingSystem.Api.Features.Rooms;
using BookingSystem.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BookingSystem.IntegrationTests.Features.Rooms;

[Collection(ApiCollection.Name)]
public sealed class GetRoomScheduleTests(ApiFactory factory)
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task Schedule_WithoutAToken_Returns401()
    {
        var room = await factory.CreateRoomAsync();

        var response = await factory.CreateClient().GetAsync(Endpoint(room, Today));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Schedule_ForAColdDate_ReturnsTheRoomsGrid()
    {
        var room = await factory.CreateRoomAsync();
        var date = Today.AddDays(1);
        var client = await factory.CreateUserClientAsync();

        var schedule = await client.GetFromJsonAsync<GetRoomSchedule.Response>(Endpoint(room, date));

        Assert.NotNull(schedule);
        Assert.Equal(room.Id, schedule.RoomId);
        Assert.Equal(date, schedule.Date);
        Assert.Equal(8, schedule.Slots.Length);
        Assert.Equal(date.ToDateTime(RoomData.Opens), schedule.Slots[0].StartsAtUtc);
        Assert.Equal(date.ToDateTime(RoomData.Closes), schedule.Slots[^1].EndsAtUtc);
    }

    [Fact]
    public async Task Schedule_ReadTwice_ReturnsTheSameSlots()
    {
        var room = await factory.CreateRoomAsync();
        var date = Today.AddDays(2);
        var client = await factory.CreateUserClientAsync();

        var first = await client.GetFromJsonAsync<GetRoomSchedule.Response>(Endpoint(room, date));
        var second = await client.GetFromJsonAsync<GetRoomSchedule.Response>(Endpoint(room, date));

        // Reading is what creates the rows, so a second read proves it creates them once.
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Slots.Select(slot => slot.SlotId), second.Slots.Select(slot => slot.SlotId));
        Assert.Equal(8, await factory.CountSlotsAsync(room));
    }

    [Fact]
    public async Task Schedule_WithNoDate_AnswersForToday()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();

        var schedule = await client.GetFromJsonAsync<GetRoomSchedule.Response>(
            $"/api/rooms/{room.Id}/schedule");

        Assert.NotNull(schedule);
        Assert.Equal(Today, schedule.Date);
    }

    [Fact]
    public async Task Schedule_ForYesterday_Returns400AndWritesNothing() =>
        await AssertRefused(Today.AddDays(-1));

    [Fact]
    public async Task Schedule_BeyondTheBookingWindow_Returns400AndWritesNothing() =>
        await AssertRefused(Today.AddDays(BookingWindowDays + 1));

    [Fact]
    public async Task Schedule_ForAnUnknownRoom_Returns404()
    {
        var client = await factory.CreateUserClientAsync();

        var response = await client.GetAsync($"/api/rooms/{Guid.NewGuid()}/schedule");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Schedule_ForADeactivatedRoom_Returns404()
    {
        var room = await factory.CreateRoomAsync(isActive: false);
        var client = await factory.CreateUserClientAsync();

        var response = await client.GetAsync(Endpoint(room, Today));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task AssertRefused(DateOnly date)
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();

        var response = await client.GetAsync(Endpoint(room, date));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await factory.CountSlotsAsync(room));
    }

    // Read from the running host rather than from SchedulingOptions' default, so configuring
    // a different window moves the test with it instead of leaving it asserting the old one.
    private int BookingWindowDays =>
        factory.Services.GetRequiredService<IOptions<SchedulingOptions>>().Value.BookingWindowDays;

    private static string Endpoint(Room room, DateOnly date) =>
        $"/api/rooms/{room.Id}/schedule?date={date:yyyy-MM-dd}";
}
