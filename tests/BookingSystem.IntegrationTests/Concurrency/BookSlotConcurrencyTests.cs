using System.Net.Http.Json;
using System.Net;
using BookingSystem.Api.Data;
using BookingSystem.Api.Domain;
using BookingSystem.Api.Features.Auth;
using BookingSystem.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace BookingSystem.IntegrationTests.Concurrency;

/// <summary>
/// The graded requirement: simultaneous requests for one slot produce exactly one booking, and
/// every loser gets a conflict rather than an error.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class BookSlotConcurrencyTests(ApiFactory factory)
{
    private const int Racers = 8;

    [Fact]
    public async Task BookSlot_RequestedSimultaneously_CreatesExactlyOneBooking()
    {
        var slot = await CreateFutureSlotAsync();

        var clients = await Task.WhenAll(
            Enumerable.Range(0, Racers).Select(_ => factory.CreateUserClientAsync()));

        // LongRunning and the gate are load-bearing: awaited directly, or via Task.Run, xUnit's
        // synchronization context runs these almost in sequence and they never overlap.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = clients
            .Select(client => Task.Factory.StartNew(
                async () =>
                {
                    await gate.Task;
                    var response = await client.PostAsJsonAsync("/api/bookings", new { slotId = slot.Id });

                    return response.StatusCode;
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap())
            .ToList();

        gate.SetResult();
        var statuses = await Task.WhenAll(attempts);

        Assert.Equal(1, statuses.Count(status => status == HttpStatusCode.Created));
        Assert.Equal(Racers - 1, statuses.Count(status => status == HttpStatusCode.Conflict));
        Assert.DoesNotContain(statuses, status => (int)status >= 500);
        Assert.Equal(1, await factory.CountBookingsAsync(slot));
        Assert.NotNull(await factory.ReadClaimAsync(slot));
    }

    [Fact]
    public async Task BookSlot_WhenAnotherRequestClaimsFirst_AnswersConflict()
    {
        var slot = await CreateFutureSlotAsync();
        var rivalUserId = await CreateUserIdAsync();
        var connectionString = ConnectionString();

        // The parallel test above can only hope to collide; this one guarantees it, by
        // committing a rival claim from another connection inside the window between the
        // handler's read and its write. EF's interception is the seam, so no production code
        // knows it is being tested.
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddDbContext<AppDbContext>(options => options
                    .UseSqlServer(connectionString)
                    .AddInterceptors(new RivalClaimant(connectionString, slot.Id, rivalUserId)))));

        var client = await host.CreateUserClientAsync();
        var response = await client.PostAsJsonAsync("/api/bookings", new { slotId = slot.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await factory.CountBookingsAsync(slot));
        Assert.Equal(rivalUserId, await ReadHolderAsync(slot));
    }

    private async Task<Slot> CreateFutureSlotAsync()
    {
        var room = await factory.CreateRoomAsync();

        return await factory.AddSlotAsync(room, DateTime.UtcNow.Date.AddDays(1).AddHours(10));
    }

    private async Task<Guid> CreateUserIdAsync()
    {
        var client = await factory.CreateUserClientAsync();
        var me = await client.GetFromJsonAsync<GetCurrentUser.Response>("/api/auth/me");

        return me!.UserId;
    }

    private async Task<Guid> ReadHolderAsync(Slot slot)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await database.Bookings
            .Where(booking => booking.SlotId == slot.Id && booking.CancelledAtUtc == null)
            .Select(booking => booking.UserId)
            .SingleAsync();
    }

    private string ConnectionString()
    {
        using var scope = factory.Services.CreateScope();

        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString()!;
    }

    /// <summary>
    /// Claims the slot on its own connection, committed, just before the intercepted context
    /// writes its own claim.
    /// </summary>
    private sealed class RivalClaimant(string connectionString, Guid slotId, Guid rivalUserId)
        : SaveChangesInterceptor
    {
        private bool _claimed;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var claimsOurSlot = eventData.Context?.ChangeTracker
                .Entries<Slot>()
                .Any(entry => entry.Entity.Id == slotId && entry.State == EntityState.Modified) ?? false;

            if (_claimed || !claimsOurSlot)
            {
                return result;
            }

            _claimed = true;

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            await using var rival = new AppDbContext(options);
            var bookingId = Guid.CreateVersion7();

            var slot = await rival.Slots.SingleAsync(candidate => candidate.Id == slotId, cancellationToken);
            slot.CurrentBookingId = bookingId;
            await rival.SaveChangesAsync(cancellationToken);

            rival.Bookings.Add(new Booking
            {
                Id = bookingId,
                SlotId = slotId,
                UserId = rivalUserId,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await rival.SaveChangesAsync(cancellationToken);

            return result;
        }
    }
}
