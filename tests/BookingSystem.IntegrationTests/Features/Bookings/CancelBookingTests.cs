using System.Net;
using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Features.Bookings;

[Collection(ApiCollection.Name)]
public sealed class CancelBookingTests(ApiFactory factory)
{
    [Fact]
    public async Task Cancel_WithoutAToken_Returns401()
    {
        var response = await factory.CreateClient().CancelAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_AsTheOwner_FreesTheSlotAndKeepsTheRow()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();
        var booking = await client.BookFirstFreeSlotAsync(room.Id, BookingData.Tomorrow);

        var response = await client.CancelAsync(booking.BookingId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await factory.ReadClaimAsync(booking.SlotId));
        Assert.Equal(0, await factory.CountLiveBookingsAsync(booking.SlotId));
        Assert.Equal(1, await factory.CountBookingsAsync(booking.SlotId));
    }

    [Fact]
    public async Task Cancel_Twice_Succeeds()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();
        var booking = await client.BookFirstFreeSlotAsync(room.Id, BookingData.Tomorrow);

        (await client.CancelAsync(booking.BookingId)).EnsureSuccessStatusCode();
        var again = await client.CancelAsync(booking.BookingId);

        // The caller asked for the booking not to stand, and it does not stand.
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(1, await factory.CountBookingsAsync(booking.SlotId));
    }

    [Fact]
    public async Task Cancel_SomebodyElsesBooking_Returns403()
    {
        var room = await factory.CreateRoomAsync();
        var owner = await factory.CreateUserClientAsync();
        var stranger = await factory.CreateUserClientAsync();
        var booking = await owner.BookFirstFreeSlotAsync(room.Id, BookingData.Tomorrow);

        var response = await stranger.CancelAsync(booking.BookingId);

        // Broken object level authorization if this passed: a regular user may cancel bookings,
        // so only the ownership check stands between a stranger and somebody else's meeting.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(booking.BookingId, await factory.ReadClaimAsync(booking.SlotId));
    }

    [Fact]
    public async Task Cancel_SomebodyElsesBooking_AsAnAdmin_Succeeds()
    {
        var room = await factory.CreateRoomAsync();
        var owner = await factory.CreateUserClientAsync();
        var admin = await factory.CreateAdminClientAsync();
        var booking = await owner.BookFirstFreeSlotAsync(room.Id, BookingData.Tomorrow);

        var response = await admin.CancelAsync(booking.BookingId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await factory.ReadClaimAsync(booking.SlotId));
    }

    [Fact]
    public async Task Cancel_AnUnknownBooking_Returns404()
    {
        var client = await factory.CreateUserClientAsync();

        var response = await client.CancelAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_ABookingWhoseSlotHasStarted_Returns400(bool asAdmin)
    {
        var room = await factory.CreateRoomAsync();
        var owner = await factory.CreateUserClientAsync();
        var booking = await owner.BookFirstFreeSlotAsync(room.Id, BookingData.Tomorrow);
        var client = asAdmin ? await factory.CreateAdminClientAsync() : owner;

        // Booking a started slot is refused, so the slot is moved after the booking is made.
        await factory.MoveSlotAsync(booking.SlotId, DateTime.UtcNow.AddHours(-2));
        var response = await client.CancelAsync(booking.BookingId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(booking.BookingId, await factory.ReadClaimAsync(booking.SlotId));
        Assert.Equal(1, await factory.CountLiveBookingsAsync(booking.SlotId));
    }
}
