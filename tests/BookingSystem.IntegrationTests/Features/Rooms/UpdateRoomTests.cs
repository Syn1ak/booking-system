using System.Net;
using System.Net.Http.Json;
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
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        Assert.NotNull(problem);
        Assert.Contains("ClosesAtUtc", problem.Errors.Keys);
    }

    [Fact]
    public async Task Update_WithASlotLengthInTheBody_IgnoresIt()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateAdminClientAsync();

        var response = await client.PutAsJsonAsync($"/api/rooms/{room.Id}", new
        {
            name = "Board room",
            opensAtUtc = "09:00:00",
            closesAtUtc = "17:00:00",
            slotLengthMinutes = 15,
        });

        // Changing slot length is refused while future bookings exist, and that check cannot
        // exist yet - so the field must not be quietly honoured in the meantime.
        var updated = await response.Content.ReadFromJsonAsync<UpdateRoom.Response>();
        Assert.NotNull(updated);
        Assert.Equal(60, updated.SlotLengthMinutes);
    }

    private static string Schedule(Guid roomId, DateOnly date) =>
        $"/api/rooms/{roomId}/schedule?date={date:yyyy-MM-dd}";
}
