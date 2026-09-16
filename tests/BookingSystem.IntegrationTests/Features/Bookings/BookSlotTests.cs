using System.Net;
using System.Net.Http.Json;
using BookingSystem.Api.Features.Bookings;
using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Features.Bookings;

[Collection(ApiCollection.Name)]
public sealed class BookSlotTests(ApiFactory factory)
{
    [Fact]
    public async Task Book_WithoutAToken_Returns401()
    {
        var response = await factory.CreateClient().BookAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Book_AFreeSlot_Returns201AndClaimsIt()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();
        var schedule = await client.ReadScheduleAsync(room.Id, BookingData.Tomorrow);
        var slot = schedule.Slots[0];

        var booking = await client.BookSucceedsAsync(slot.SlotId);

        Assert.Equal(slot.SlotId, booking.SlotId);
        Assert.Equal(room.Id, booking.RoomId);
        Assert.Equal(slot.StartsAtUtc, booking.StartsAtUtc);
        Assert.Equal(booking.BookingId, await factory.ReadClaimAsync(slot.SlotId));
        Assert.Equal(1, await factory.CountLiveBookingsAsync(slot.SlotId));
    }

    [Fact]
    public async Task Book_TheSameSlotTwiceAsTheSameUser_Returns200WithTheSameBooking()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();
        var first = await client.BookFirstFreeSlotAsync(room.Id, BookingData.Tomorrow);

        var response = await client.BookAsync(first.SlotId);
        var again = await response.Content.ReadFromJsonAsync<BookSlot.Response>();

        // A double-click has already achieved what it asked for.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(again);
        Assert.Equal(first.BookingId, again.BookingId);
        Assert.Equal(1, await factory.CountBookingsAsync(first.SlotId));
    }

    [Fact]
    public async Task Book_ASlotSomebodyElseHolds_Returns409()
    {
        var room = await factory.CreateRoomAsync();
        var owner = await factory.CreateUserClientAsync();
        var stranger = await factory.CreateUserClientAsync();
        var booking = await owner.BookFirstFreeSlotAsync(room.Id, BookingData.Tomorrow);

        var response = await stranger.BookAsync(booking.SlotId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await factory.CountBookingsAsync(booking.SlotId));
        Assert.Equal(booking.BookingId, await factory.ReadClaimAsync(booking.SlotId));
    }

    [Fact]
    public async Task Book_ASlotThatHasAlreadyStarted_Returns400()
    {
        var room = await factory.CreateRoomAsync();
        var started = await factory.AddSlotAsync(room, DateTime.UtcNow.AddHours(-1));
        var client = await factory.CreateUserClientAsync();

        var response = await client.BookAsync(started.Id);

        // 400 rather than 409, so that 409 means only that somebody else won the race.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await factory.ReadClaimAsync(started.Id));
    }

    [Fact]
    public async Task Book_AnUnknownSlot_Returns404()
    {
        var client = await factory.CreateUserClientAsync();

        var response = await client.BookAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Book_ASlotOfADeactivatedRoom_Returns404()
    {
        var room = await factory.CreateRoomAsync(isActive: false);
        var slot = await factory.AddSlotAsync(room, DateTime.UtcNow.AddDays(1));
        var client = await factory.CreateUserClientAsync();

        var response = await client.BookAsync(slot.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(await factory.ReadClaimAsync(slot.Id));
    }

    [Fact]
    public async Task Book_ASlot_ShowsItBookedToEveryoneAndOwnedOnlyByTheHolder()
    {
        var room = await factory.CreateRoomAsync();
        var owner = await factory.CreateUserClientAsync();
        var onlooker = await factory.CreateUserClientAsync();
        var booking = await owner.BookFirstFreeSlotAsync(room.Id, BookingData.Tomorrow);

        var mine = await owner.ReadScheduleAsync(room.Id, BookingData.Tomorrow);
        var theirs = await onlooker.ReadScheduleAsync(room.Id, BookingData.Tomorrow);

        var asOwner = mine.Slots.Single(slot => slot.SlotId == booking.SlotId);
        var asOnlooker = theirs.Slots.Single(slot => slot.SlotId == booking.SlotId);

        Assert.True(asOwner.IsBooked);
        Assert.Equal(booking.BookingId, asOwner.MyBookingId);
        Assert.True(asOnlooker.IsBooked);
        Assert.Null(asOnlooker.MyBookingId);
    }
}
