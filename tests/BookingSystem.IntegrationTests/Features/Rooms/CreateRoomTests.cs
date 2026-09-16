using System.Net;
using System.Net.Http.Json;
using BookingSystem.Api.Features.Rooms;
using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Features.Rooms;

[Collection(ApiCollection.Name)]
public sealed class CreateRoomTests(ApiFactory factory)
{
    private const string Endpoint = "/api/rooms";

    private static readonly object Valid = new
    {
        name = "Board room",
        opensAtUtc = "09:00:00",
        closesAtUtc = "17:00:00",
        slotLengthMinutes = 60,
    };

    [Fact]
    public async Task Create_WithoutAToken_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(Endpoint, Valid);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAUser_Returns403()
    {
        var client = await factory.CreateUserClientAsync();

        var response = await client.PostAsJsonAsync(Endpoint, Valid);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAnAdmin_Returns201AndTheRoomIsReadableAtItsLocation()
    {
        var client = await factory.CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync(Endpoint, Valid);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateRoom.Response>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.RoomId);

        // The Location header is part of the contract, so it is followed rather than assumed.
        var fetched = await client.GetFromJsonAsync<GetRoom.Response>(response.Headers.Location);
        Assert.NotNull(fetched);
        Assert.Equal(created.RoomId, fetched.RoomId);
        Assert.Equal(60, fetched.SlotLengthMinutes);
    }

    [Theory]
    [InlineData("", "09:00:00", "17:00:00", 60, "Name")]
    [InlineData("Odd grid", "09:00:00", "17:00:00", 37, "SlotLengthMinutes")]
    [InlineData("Backwards", "17:00:00", "09:00:00", 60, "ClosesAtUtc")]
    [InlineData("Too short", "09:00:00", "09:30:00", 60, "ClosesAtUtc")]
    public async Task Create_WithAnInvalidRequest_Returns400NamingTheField(
        string name, string opens, string closes, int slotLength, string expectedField)
    {
        var client = await factory.CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync(Endpoint, new
        {
            name,
            opensAtUtc = opens,
            closesAtUtc = closes,
            slotLengthMinutes = slotLength,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
        Assert.NotNull(problem);
        Assert.Contains(expectedField, problem.Errors.Keys);
    }
}
