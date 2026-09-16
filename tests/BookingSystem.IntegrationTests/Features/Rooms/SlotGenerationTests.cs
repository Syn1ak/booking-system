using BookingSystem.Api.Data;
using BookingSystem.Api.Domain;
using BookingSystem.Api.Features.Rooms;
using BookingSystem.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BookingSystem.IntegrationTests.Features.Rooms;

/// <summary>
/// Generation has no endpoint yet, so it is driven through the container rather than over
/// HTTP - but against real SQL Server, since the behaviour under test is a unique index.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SlotGenerationTests(ApiFactory factory)
{
    private static readonly TimeOnly Opens = new(9, 0);
    private static readonly TimeOnly Closes = new(17, 0);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task EnsureSlots_OnAColdDate_MaterialisesTheRoomsGrid()
    {
        var room = await GivenRoom(slotLengthMinutes: 60);
        var date = Today.AddDays(1);

        var slots = await EnsureSlots(room, date);

        Assert.NotNull(slots);
        Assert.Equal(8, slots.Count);
        Assert.Equal(date.ToDateTime(Opens), slots[0].StartsAtUtc);
        Assert.Equal(date.ToDateTime(Closes), slots[^1].EndsAtUtc);
        Assert.All(slots, slot => Assert.Equal(TimeSpan.FromHours(1), slot.EndsAtUtc - slot.StartsAtUtc));
    }

    [Fact]
    public async Task EnsureSlots_WhenTheDayDoesNotDivideEvenly_DropsTheTrailingRemainder()
    {
        var room = await GivenRoom(slotLengthMinutes: 45);
        var date = Today.AddDays(1);

        var slots = await EnsureSlots(room, date);

        // 09:00-17:00 is ten 45-minute slots and a dropped 30-minute remainder.
        Assert.NotNull(slots);
        Assert.Equal(10, slots.Count);
        Assert.Equal(date.ToDateTime(new TimeOnly(16, 30)), slots[^1].EndsAtUtc);
    }

    [Fact]
    public async Task EnsureSlots_CalledTwice_InsertsNothingTheSecondTime()
    {
        var room = await GivenRoom(slotLengthMinutes: 60);
        var date = Today.AddDays(2);

        var first = await EnsureSlots(room, date);
        var second = await EnsureSlots(room, date);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Select(slot => slot.Id), second.Select(slot => slot.Id));
        Assert.Equal(8, await CountSlots(room));
    }

    [Fact]
    public async Task EnsureSlots_CalledConcurrently_LeavesExactlyOneSetOfRows()
    {
        var room = await GivenRoom(slotLengthMinutes: 60);
        var date = Today.AddDays(3);

        // LongRunning and the gate are load-bearing: awaited directly, or via Task.Run, xUnit's
        // synchronization context runs these almost in sequence and they never overlap.
        // Collisions are still the OS's decision, so this asserts the outcome; the swallow
        // itself is covered by the test below.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = Enumerable.Range(0, 8)
            .Select(_ => Task.Factory.StartNew(
                async () =>
                {
                    await gate.Task;
                    return await EnsureSlots(room, date);
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap())
            .ToList();

        gate.SetResult();
        var results = await Task.WhenAll(attempts);

        Assert.All(results, slots => Assert.Equal(8, Assert.IsAssignableFrom<IReadOnlyList<Slot>>(slots).Count));
        Assert.Equal(8, await CountSlots(room));
    }

    [Fact]
    public async Task EnsureSlots_WhenAnotherWriterCommitsFirst_ReturnsTheWinnersRows()
    {
        var room = await GivenRoom(slotLengthMinutes: 60);
        var date = Today.AddDays(4);

        // The parallel test can only hope to collide; this one guarantees it.
        var slots = await EnsureSlotsAgainstRivalWriter(room, date);

        Assert.NotNull(slots);
        Assert.Equal(8, slots.Count);
        Assert.Equal(8, await CountSlots(room));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(15)]
    public async Task EnsureSlots_OutsideTheBookingWindow_ReturnsNullAndWritesNothing(int daysFromToday)
    {
        var room = await GivenRoom(slotLengthMinutes: 60);

        var slots = await EnsureSlots(room, Today.AddDays(daysFromToday));

        Assert.Null(slots);
        Assert.Equal(0, await CountSlots(room));
    }

    private async Task<Room> GivenRoom(int slotLengthMinutes)
    {
        // A room per test: the assembly shares one database.
        var room = new Room
        {
            Name = $"Test room {Guid.NewGuid():N}",
            OpensAtUtc = Opens,
            ClosesAtUtc = Closes,
            SlotLengthMinutes = slotLengthMinutes,
        };

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        database.Rooms.Add(room);
        await database.SaveChangesAsync();

        return room;
    }

    private async Task<IReadOnlyList<Slot>?> EnsureSlots(Room room, DateOnly date)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<SlotGenerator>()
            .EnsureSlotsAsync(room, date);
    }

    /// <summary>
    /// Runs generation against a context whose save is preceded by a rival writer committing
    /// the same rows on its own connection - the exact window the swallow exists for. EF's own
    /// interception is the seam, so no production code knows it is being tested.
    /// </summary>
    private async Task<IReadOnlyList<Slot>?> EnsureSlotsAgainstRivalWriter(Room room, DateOnly date)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString())
            .AddInterceptors(new RivalWriter(ConnectionString(), room, date))
            .Options;

        await using var database = new AppDbContext(options);
        var generator = new SlotGenerator(
            database,
            Options.Create(new SchedulingOptions()),
            TimeProvider.System);

        return await generator.EnsureSlotsAsync(room, date);
    }

    private string ConnectionString()
    {
        using var scope = factory.Services.CreateScope();

        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString()!;
    }

    private sealed class RivalWriter(string connectionString, Room room, DateOnly date) : SaveChangesInterceptor
    {
        private bool _written;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_written)
            {
                return result;
            }

            _written = true;

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            await using var rival = new AppDbContext(options);
            rival.Slots.AddRange(room.SlotsOn(date).Select(boundary => new Slot
            {
                RoomId = room.Id,
                StartsAtUtc = boundary.StartsAtUtc,
                EndsAtUtc = boundary.EndsAtUtc,
            }));

            await rival.SaveChangesAsync(cancellationToken);

            return result;
        }
    }

    private async Task<int> CountSlots(Room room)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Slots.CountAsync(slot => slot.RoomId == room.Id);
    }
}
