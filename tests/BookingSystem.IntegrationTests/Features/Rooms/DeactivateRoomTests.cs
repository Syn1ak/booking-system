using System.Net;
using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Features.Rooms;

[Collection(ApiCollection.Name)]
public sealed class DeactivateRoomTests(ApiFactory factory)
{
    [Fact]
    public async Task Deactivate_AsAUser_Returns403()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();

        var response = await client.DeleteAsync($"/api/rooms/{room.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_ForAnUnknownRoom_Returns404()
    {
        var client = await factory.CreateAdminClientAsync();

        var response = await client.DeleteAsync($"/api/rooms/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Deactivate_AsAnAdmin_HidesTheRoomFromReads()
    {
        var room = await factory.CreateRoomAsync();
        var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateUserClientAsync();

        var response = await admin.DeleteAsync($"/api/rooms/{room.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.GetAsync($"/api/rooms/{room.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await user.GetAsync($"/api/rooms/{room.Id}/schedule")).StatusCode);
    }

    [Fact]
    public async Task Deactivate_Twice_Returns204()
    {
        var room = await factory.CreateRoomAsync();
        var admin = await factory.CreateAdminClientAsync();

        await admin.DeleteAsync($"/api/rooms/{room.Id}");
        var again = await admin.DeleteAsync($"/api/rooms/{room.Id}");

        // A retried delete should confirm the room is gone, not fail because it already is.
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    [Fact]
    public async Task Deactivate_KeepsTheRoomsSlotRows()
    {
        var room = await factory.CreateRoomAsync();
        var user = await factory.CreateUserClientAsync();
        (await user.GetAsync($"/api/rooms/{room.Id}/schedule")).EnsureSuccessStatusCode();
        var admin = await factory.CreateAdminClientAsync();

        await admin.DeleteAsync($"/api/rooms/{room.Id}");

        // Deactivation is reversible and past bookings stay readable, so nothing is deleted.
        Assert.Equal(8, await factory.CountSlotsAsync(room));
    }
}
