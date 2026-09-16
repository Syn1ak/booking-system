using System.Net;
using System.Net.Http.Json;
using BookingSystem.Api.Features.Bookings;
using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Features.Bookings;

[Collection(ApiCollection.Name)]
public sealed class ListAllBookingsTests(ApiFactory factory)
{
    private const string Route = "/api/admin/bookings";

    [Fact]
    public async Task List_WithoutAToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync(Route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_AsAUser_Returns403()
    {
        var client = await factory.CreateUserClientAsync();

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_AsAnAdmin_NamesTheUserBehindEachBooking()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();
        var admin = await factory.CreateAdminClientAsync();
        var booking = await client.BookFirstFreeSlotAsync(room.Id, BookingData.Tomorrow);
        var email = (await client.GetFromJsonAsync<Api.Features.Auth.GetCurrentUser.Response>("/api/auth/me"))!;

        var listed = (await ListAsync(admin, $"?roomId={room.Id}")).Single();

        // The whole point of the endpoint, and why it is the only one that carries an identity.
        Assert.Equal(booking.BookingId, listed.BookingId);
        Assert.Equal(email.UserId, listed.UserId);
        Assert.Equal(email.Email, listed.UserEmail);
    }

    [Fact]
    public async Task List_FiltersByRoom()
    {
        var wanted = await factory.CreateRoomAsync();
        var other = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();
        var admin = await factory.CreateAdminClientAsync();
        var booking = await client.BookFirstFreeSlotAsync(wanted.Id, BookingData.Tomorrow);
        await client.BookFirstFreeSlotAsync(other.Id, BookingData.Tomorrow);

        var listed = await ListAsync(admin, $"?roomId={wanted.Id}");

        Assert.Equal([booking.BookingId], listed.Select(row => row.BookingId));
    }

    [Fact]
    public async Task List_ExcludingCancelled_LeavesThemOut()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();
        var admin = await factory.CreateAdminClientAsync();
        var schedule = await client.ReadScheduleAsync(room.Id, BookingData.Tomorrow);
        var standing = await client.BookSucceedsAsync(schedule.Slots[0].SlotId);
        var cancelled = await client.BookSucceedsAsync(schedule.Slots[1].SlotId);
        (await client.CancelAsync(cancelled.BookingId)).EnsureSuccessStatusCode();

        var byDefault = await ListAsync(admin, $"?roomId={room.Id}");
        var live = await ListAsync(admin, $"?roomId={room.Id}&includeCancelled=false");

        Assert.Equal(2, byDefault.Length);
        Assert.Equal([standing.BookingId], live.Select(row => row.BookingId));
    }

    [Fact]
    public async Task List_FilteredToADate_IncludesThatWholeDay()
    {
        var room = await factory.CreateRoomAsync();
        var client = await factory.CreateUserClientAsync();
        var admin = await factory.CreateAdminClientAsync();
        var date = BookingData.Tomorrow;
        var booking = await client.BookFirstFreeSlotAsync(room.Id, date);

        var onTheDay = await ListAsync(admin, $"?roomId={room.Id}&from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}");
        var theDayBefore = await ListAsync(
            admin, $"?roomId={room.Id}&from={date.AddDays(-1):yyyy-MM-dd}&to={date.AddDays(-1):yyyy-MM-dd}");

        // `to` names a day the caller expects to see, not a boundary that excludes it.
        Assert.Equal([booking.BookingId], onTheDay.Select(row => row.BookingId));
        Assert.Empty(theDayBefore);
    }

    private static async Task<ListAllBookings.Response[]> ListAsync(HttpClient client, string query = "") =>
        (await client.GetFromJsonAsync<ListAllBookings.Response[]>(Route + query))!;
}
