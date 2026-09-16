using BookingSystem.Api.Data;
using BookingSystem.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookingSystem.IntegrationTests.Fixtures;

internal static class BookingData
{
    public static async Task<int> CountLiveBookingsAsync(this ApiFactory factory, Slot slot)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Bookings.CountAsync(booking => booking.SlotId == slot.Id && booking.CancelledAtUtc == null);
    }

    public static async Task<int> CountBookingsAsync(this ApiFactory factory, Slot slot)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Bookings.CountAsync(booking => booking.SlotId == slot.Id);
    }

    public static async Task<Guid?> ReadClaimAsync(this ApiFactory factory, Slot slot)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Slots.Where(candidate => candidate.Id == slot.Id)
            .Select(candidate => candidate.CurrentBookingId)
            .SingleAsync();
    }
}
