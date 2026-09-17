using System.Net;
using System.Net.Http.Json;
using BookingSystem.Api.Features.Bookings;
using BookingSystem.Api.Features.Rooms;
using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Features.Rooms;

[Collection(ApiCollection.Name)]
public sealed class UpdateRoomTests(ApiFactory factory)
{
    private static readonly object NarrowerHours = new
    {
        name = "Board room",
        opensAtUtc = "10:00:00",
        closesAtUtc = "14:00:00",
        slotLengthMinutes = 60,
    };

    [Fact]
    public async Task Update_AsAUser_Returns403()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();

        var response = await client.PutAsJsonAsync($"/api/rooms/{room.Id}", NarrowerHours);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_ForAnUnknownRoom_Returns404()
    {
        var client = await factory.CreateAdminClientAsync();

        var response = await client.PutAsJsonAsync($"/api/rooms/{Guid.NewGuid()}", NarrowerHours);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WhenHoursChange_RebuildsTheGridOnTheNextRead()
    {
        var room = await factory.CreateRoomAsync();
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var user = await factory.CreateUserClientAsync();
        var admin = await factory.CreateAdminClientAsync();

        var before = await user.GetFromJsonAsync<GetRoomSchedule.Response>(Schedule(room.Id, date));
        (await admin.PutAsJsonAsync($"/api/rooms/{room.Id}", NarrowerHours)).EnsureSuccessStatusCode();
        var after = await user.GetFromJsonAsync<GetRoomSchedule.Response>(Schedule(room.Id, date));

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal(8, before.Slots.Length);
        Assert.Equal(4, after.Slots.Length);
        Assert.Equal(date.ToDateTime(new TimeOnly(10, 0)), after.Slots[0].StartsAtUtc);
        Assert.Equal(date.ToDateTime(new TimeOnly(14, 0)), after.Slots[^1].EndsAtUtc);
    }

    [Fact]
    public async Task Update_WhenHoursChange_LeavesSlotsThatHaveAlreadyStarted()
    {
        var room = await factory.CreateRoomAsync();
        var started = await factory.AddSlotAsync(room, DateTime.UtcNow.AddHours(-1));
        var admin = await factory.CreateAdminClientAsync();

        (await admin.PutAsJsonAsync($"/api/rooms/{room.Id}", NarrowerHours)).EnsureSuccessStatusCode();

        // Nobody's meeting moves because an admin edited the room's hours.
        Assert.True(await factory.SlotExistsAsync(started));
    }

    [Fact]
    public async Task Update_WithADayTooShortForTheRoomsSlot_Returns400()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateAdminClientAsync();

        var response = await client.PutAsJsonAsync($"/api/rooms/{room.Id}", new
        {
            name = "Board room",
            opensAtUtc = "09:00:00",
            closesAtUtc = "09:30:00",
            slotLengthMinutes = 60,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        Assert.NotNull(problem);
        Assert.Contains("ClosesAtUtc", problem.Errors.Keys);
    }

    [Fact]
    public async Task Update_WithANewSlotLength_WhenNothingIsBooked_RebuildsTheGrid()
    {
        var room = await factory.CreateRoomAsync();
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var user = await factory.CreateUserClientAsync();
        var admin = await factory.CreateAdminClientAsync();

        var response = await admin.PutAsJsonAsync($"/api/rooms/{room.Id}", new
        {
            name = "Board room",
            opensAtUtc = "09:00:00",
            closesAtUtc = "17:00:00",
            slotLengthMinutes = 30,
        });

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<UpdateRoom.Response>();
        var after = await user.GetFromJsonAsync<GetRoomSchedule.Response>(Schedule(room.Id, date));

        Assert.NotNull(updated);
        Assert.Equal(30, updated.SlotLengthMinutes);
        Assert.NotNull(after);
        Assert.Equal(16, after.Slots.Length);
    }

    [Fact]
    public async Task Update_WithANewSlotLength_WhileABookingStands_Returns409()
    {
        var room = await factory.CreateRoomAsync();
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var user = await factory.CreateUserClientAsync();
        var admin = await factory.CreateAdminClientAsync();

        var schedule = await user.GetFromJsonAsync<GetRoomSchedule.Response>(Schedule(room.Id, date));
        Assert.NotNull(schedule);
        (await user.PostAsJsonAsync("/api/bookings", new { slotId = schedule.Slots[0].SlotId }))
            .EnsureSuccessStatusCode();

        var response = await admin.PutAsJsonAsync($"/api/rooms/{room.Id}", new
        {
            name = "Renamed",
            opensAtUtc = "09:00:00",
            closesAtUtc = "17:00:00",
            slotLengthMinutes = 30,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("1 future booking", await response.Content.ReadAsStringAsync());

        // The whole request is refused, not just the slot length: the name is unchanged too.
        var unchanged = await admin.GetFromJsonAsync<GetRoom.Response>($"/api/rooms/{room.Id}");
        Assert.NotNull(unchanged);
        Assert.Equal(60, unchanged.SlotLengthMinutes);
        Assert.Equal(room.Name, unchanged.Name);
    }

    [Fact]
    public async Task Update_WhenHoursChange_AfterABookingWasCancelled_RebuildsTheGridAndKeepsTheHistory()
    {
        var room = await factory.CreateRoomAsync();
        var date = BookingData.Tomorrow;
        var user = await factory.CreateUserClientAsync();
        var admin = await factory.CreateAdminClientAsync();

        var booking = await user.BookFirstFreeSlotAsync(room.Id, date);
        (await user.CancelAsync(booking.BookingId)).EnsureSuccessStatusCode();

        var response = await admin.PutAsJsonAsync($"/api/rooms/{room.Id}", NarrowerHours);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await user.ReadScheduleAsync(room.Id, date);
        Assert.Equal(4, after.Slots.Length);
        Assert.Equal(date.ToDateTime(new TimeOnly(10, 0)), after.Slots[0].StartsAtUtc);

        // Cancelling keeps the row so admins can still read it; changing hours must not lose it.
        var history = await admin.GetFromJsonAsync<ListAllBookings.Response[]>(
            $"/api/admin/bookings?roomId={room.Id}");
        Assert.NotNull(history);
        var cancelled = Assert.Single(history);
        Assert.Equal(booking.BookingId, cancelled.BookingId);
        Assert.Equal(booking.StartsAtUtc, cancelled.StartsAtUtc);
        Assert.NotNull(cancelled.CancelledAtUtc);
    }

    [Fact]
    public async Task Update_WithANewSlotLength_WhenOnlyCancelledBookingsRemain_RebuildsTheGrid()
    {
        var room = await factory.CreateRoomAsync();
        var date = BookingData.Tomorrow;
        var user = await factory.CreateUserClientAsync();
        var admin = await factory.CreateAdminClientAsync();

        var booking = await user.BookFirstFreeSlotAsync(room.Id, date);
        (await user.CancelAsync(booking.BookingId)).EnsureSuccessStatusCode();

        var response = await admin.PutAsJsonAsync($"/api/rooms/{room.Id}", new
        {
            name = "Board room",
            opensAtUtc = "09:00:00",
            closesAtUtc = "17:00:00",
            slotLengthMinutes = 30,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(16, (await user.ReadScheduleAsync(room.Id, date)).Slots.Length);
    }

    [Fact]
    public async Task Book_ASlotRetiredByAnHoursChange_Returns404()
    {
        var room = await factory.CreateRoomAsync();
        var date = BookingData.Tomorrow;
        var user = await factory.CreateUserClientAsync();
        var admin = await factory.CreateAdminClientAsync();

        var booking = await user.BookFirstFreeSlotAsync(room.Id, date);
        (await user.CancelAsync(booking.BookingId)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/rooms/{room.Id}", NarrowerHours)).EnsureSuccessStatusCode();

        // The 09:00 row survives only as the cancelled booking's history; it is not bookable.
        var response = await user.BookAsync(booking.SlotId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await factory.CountLiveBookingsAsync(booking.SlotId));
    }

    private static string Schedule(Guid roomId, DateOnly date) =>
        $"/api/rooms/{roomId}/schedule?date={date:yyyy-MM-dd}";
}
