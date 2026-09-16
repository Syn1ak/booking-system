using System.Net;
using System.Net.Http.Json;
using BookingSystem.Api.Features.Rooms;
using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Features.Rooms;

[Collection(ApiCollection.Name)]
public sealed class GetRoomTests(ApiFactory factory)
{
    [Fact]
    public async Task Get_WithoutAToken_Returns401()
    {
        var room = await factory.CreateRoomAsync();

        var response = await factory.CreateClient().GetAsync($"/api/rooms/{room.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_AsAUser_ReturnsTheRoomsRules()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();

        var fetched = await client.GetFromJsonAsync<GetRoom.Response>($"/api/rooms/{room.Id}");

        Assert.NotNull(fetched);
        Assert.Equal(room.Id, fetched.RoomId);
        Assert.Equal(RoomData.Opens, fetched.OpensAtUtc);
        Assert.Equal(RoomData.Closes, fetched.ClosesAtUtc);
        Assert.Equal(60, fetched.SlotLengthMinutes);
    }

    [Fact]
    public async Task Get_ForADeactivatedRoom_Returns404()
    {
        var room = await factory.CreateRoomAsync(isActive: false);
        var client = await factory.CreateUserClientAsync();

        var response = await client.GetAsync($"/api/rooms/{room.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_ForAnUnknownRoom_Returns404()
    {
        var client = await factory.CreateUserClientAsync();

        var response = await client.GetAsync($"/api/rooms/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
