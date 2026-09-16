using System.Net.Http.Json;
using BookingSystem.Api.Data;
using BookingSystem.Api.Domain;
using BookingSystem.Api.Features.Bookings;
using BookingSystem.Api.Features.Rooms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookingSystem.IntegrationTests.Fixtures;

/// <summary>
/// Helpers for driving the booking endpoints, typed against the endpoints' own response records
/// so a change to a contract breaks compilation here rather than being silently tolerated.
/// </summary>
internal static class BookingData
{
    public static DateOnly Tomorrow => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

    public static async Task<GetRoomSchedule.Response> ReadScheduleAsync(
        this HttpClient client, Guid roomId, DateOnly date)
    {
        var response = await client.GetAsync($"/api/rooms/{roomId}/schedule?date={date:yyyy-MM-dd}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<GetRoomSchedule.Response>())!;
    }

    public static Task<HttpResponseMessage> BookAsync(this HttpClient client, Guid slotId) =>
        client.PostAsJsonAsync("/api/bookings", new { slotId });

    public static async Task<BookSlot.Response> BookSucceedsAsync(this HttpClient client, Guid slotId)
    {
        var response = await client.BookAsync(slotId);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<BookSlot.Response>())!;
    }

    /// <summary>Books the room's first free slot on <paramref name="date"/>.</summary>
    public static async Task<BookSlot.Response> BookFirstFreeSlotAsync(
        this HttpClient client, Guid roomId, DateOnly date)
    {
        var schedule = await client.ReadScheduleAsync(roomId, date);

        return await client.BookSucceedsAsync(schedule.Slots.First(slot => !slot.IsBooked).SlotId);
    }

    public static Task<HttpResponseMessage> CancelAsync(this HttpClient client, Guid bookingId) =>
        client.DeleteAsync($"/api/bookings/{bookingId}");

    /// <summary>
    /// Moves a slot's times, so a booking can be made through the API on a slot that has not
    /// started and then examined as one that has. The endpoints refuse to book a started slot,
    /// which is the only reason this reaches past them.
    /// </summary>
    public static async Task MoveSlotAsync(this ApiFactory factory, Guid slotId, DateTime startsAtUtc)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await database.Slots
            .Where(slot => slot.Id == slotId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(slot => slot.StartsAtUtc, startsAtUtc)
                .SetProperty(slot => slot.EndsAtUtc, startsAtUtc.AddHours(1)));
    }

    public static async Task<int> CountLiveBookingsAsync(this ApiFactory factory, Slot slot) =>
        await factory.CountLiveBookingsAsync(slot.Id);

    public static async Task<int> CountLiveBookingsAsync(this ApiFactory factory, Guid slotId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Bookings.CountAsync(booking => booking.SlotId == slotId && booking.CancelledAtUtc == null);
    }

    public static async Task<int> CountBookingsAsync(this ApiFactory factory, Slot slot) =>
        await factory.CountBookingsAsync(slot.Id);

    public static async Task<int> CountBookingsAsync(this ApiFactory factory, Guid slotId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Bookings.CountAsync(booking => booking.SlotId == slotId);
    }

    public static async Task<Guid?> ReadClaimAsync(this ApiFactory factory, Slot slot) =>
        await factory.ReadClaimAsync(slot.Id);

    public static async Task<Guid?> ReadClaimAsync(this ApiFactory factory, Guid slotId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Slots.Where(slot => slot.Id == slotId)
            .Select(slot => slot.CurrentBookingId)
            .SingleAsync();
    }
}
