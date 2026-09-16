using System.Net;
using System.Net.Http.Json;
using BookingSystem.Api.Features.Rooms;
using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Features.Rooms;

[Collection(ApiCollection.Name)]
public sealed class ListRoomsTests(ApiFactory factory)
{
    [Fact]
    public async Task List_WithoutAToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/rooms");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_AsAUser_IncludesActiveRooms()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();

        var rooms = await client.GetFromJsonAsync<ListRooms.Response[]>("/api/rooms");

        // Contains rather than equals: every test in the assembly shares one database.
        Assert.NotNull(rooms);
        Assert.Contains(rooms, listed => listed.RoomId == room.Id);
    }

    [Fact]
    public async Task List_ExcludesDeactivatedRooms()
    {
        var room = await factory.CreateRoomAsync(isActive: false);
        var client = await factory.CreateUserClientAsync();

        var rooms = await client.GetFromJsonAsync<ListRooms.Response[]>("/api/rooms");

        Assert.NotNull(rooms);
        Assert.DoesNotContain(rooms, listed => listed.RoomId == room.Id);
    }
}
