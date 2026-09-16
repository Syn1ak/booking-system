using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Concurrency;

[Collection(ApiCollection.Name)]
public sealed class SlotClaimConsistencyTests(ApiFactory factory)
{
    /// <summary>
    /// concurrency.md accepts storing availability twice - as a claim on the slot, and
    /// implicitly in the bookings table - and answers the drift that invites with one write
    /// path, a conditional release and a backstop index. This is the test that keeps that
    /// acceptance honest rather than aspirational.
    /// </summary>
    [Fact]
    public async Task ThroughBookingCancellingAndRebooking_TheClaimAndTheBookingsAgree()
    {
        var room = await factory.CreateRoomAsync();
        var date = BookingData.Tomorrow;
        var owner = await factory.CreateUserClientAsync();
        var successor = await factory.CreateUserClientAsync();

        var first = await owner.BookFirstFreeSlotAsync(room.Id, date);
        var slotId = first.SlotId;
        await AssertAgreesAsync(first.BookingId, live: 1, rows: 1);

        (await owner.CancelAsync(first.BookingId)).EnsureSuccessStatusCode();
        await AssertAgreesAsync(null, live: 0, rows: 1);

        var second = await successor.BookSucceedsAsync(slotId);
        await AssertAgreesAsync(second.BookingId, live: 1, rows: 2);

        (await successor.CancelAsync(second.BookingId)).EnsureSuccessStatusCode();
        await AssertAgreesAsync(null, live: 0, rows: 2);

        async Task AssertAgreesAsync(Guid? holder, int live, int rows)
        {
            var claim = await factory.ReadClaimAsync(slotId);
            var schedule = await owner.ReadScheduleAsync(room.Id, date);
            var slot = schedule.Slots.Single(candidate => candidate.SlotId == slotId);

            Assert.Equal(holder, claim);
            Assert.Equal(live, await factory.CountLiveBookingsAsync(slotId));
            Assert.Equal(rows, await factory.CountBookingsAsync(slotId));

            // The schedule answers from the claim alone, so a drift between the two would show
            // every viewer the wrong availability. This is where that becomes visible.
            Assert.Equal(claim is not null, slot.IsBooked);
            Assert.Equal(live, slot.IsBooked ? 1 : 0);
        }
    }
}
