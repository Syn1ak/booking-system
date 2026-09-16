using BookingSystem.Api.Data;
using BookingSystem.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookingSystem.IntegrationTests.Fixtures;

/// <summary>
/// Rooms for tests, written straight to the database rather than through the admin endpoint,
/// so a test of reading a schedule does not fail because creating a room broke.
/// </summary>
internal static class RoomData
{
    public static readonly TimeOnly Opens = new(9, 0);
    public static readonly TimeOnly Closes = new(17, 0);

    /// <summary>
    /// A room nothing else will touch. The assembly shares one database, so tests that share a
    /// room would inherit each other's generated slots.
    /// </summary>
    public static async Task<Room> CreateRoomAsync(
        this ApiFactory factory,
        int slotLengthMinutes = 60,
        bool isActive = true)
    {
        var room = new Room
        {
            Name = $"Test room {Guid.NewGuid():N}",
            OpensAtUtc = Opens,
            ClosesAtUtc = Closes,
            SlotLengthMinutes = slotLengthMinutes,
            IsActive = isActive,
        };

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.Rooms.Add(room);
        await database.SaveChangesAsync();

        return room;
    }

    public static async Task<int> CountSlotsAsync(this ApiFactory factory, Room room)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Slots.CountAsync(slot => slot.RoomId == room.Id);
    }
}
