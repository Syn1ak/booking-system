using BookingSystem.Api.Data;
using BookingSystem.Api.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BookingSystem.Api.Features.Rooms;

/// <summary>
/// Materialises a room's slot rows for a date, the first time that date is asked for.
/// Generating on read cannot run dry: it needs no always-on process, survives scale-out, and
/// self-heals if rows are lost.
/// </summary>
public sealed class SlotGenerator(
    AppDbContext database,
    IOptions<SchedulingOptions> options,
    TimeProvider clock)
{
    // SQL Server raises 2601 for a unique index and 2627 for a unique constraint, depending on
    // how the index was declared. Matching one leaves the other unhandled.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public int BookingWindowDays => options.Value.BookingWindowDays;

    /// <summary>The latest date a schedule may be read for, inclusive.</summary>
    public DateOnly LastBookableDate => Today.AddDays(BookingWindowDays);

    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    /// <summary>
    /// The room's slots on <paramref name="dateUtc"/>, generating them first if missing, or
    /// <c>null</c> if the date is outside the booking window. Null rather than an empty list:
    /// a room with no slots that day and a date closed to booking mean opposite things.
    /// </summary>
    public async Task<IReadOnlyList<Slot>?> EnsureSlotsAsync(
        Room room,
        DateOnly dateUtc,
        CancellationToken cancellationToken = default)
    {
        if (!IsWithinWindow(dateUtc))
        {
            return null;
        }

        var dayStartUtc = dateUtc.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEndUtc = dayStartUtc.AddDays(1);

        var existing = await ReadAsync();

        var missing = room.SlotsOn(dateUtc)
            .Where(boundary => existing.TrueForAll(slot => slot.StartsAtUtc != boundary.StartsAtUtc))
            .Select(boundary => new Slot
            {
                RoomId = room.Id,
                StartsAtUtc = boundary.StartsAtUtc,
                EndsAtUtc = boundary.EndsAtUtc,
            })
            .ToList();

        if (missing.Count == 0)
        {
            return existing;
        }

        database.Slots.AddRange(missing);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateSlot(exception))
        {
            // Losing this race is a success: the rows we wanted now exist. Booking turns the
            // same error into a 409 - losing a race to create a slot means it exists, losing a
            // race to book one means it is someone else's - so do not copy one path onto the
            // other. Detaching lets the re-read below return the winner's rows.
            foreach (var slot in missing)
            {
                database.Entry(slot).State = EntityState.Detached;
            }
        }

        return await ReadAsync();

        Task<List<Slot>> ReadAsync() => database.Slots
            .AsNoTracking()
            .Where(slot => slot.RoomId == room.Id
                           && slot.RetiredAtUtc == null
                           && slot.StartsAtUtc >= dayStartUtc
                           && slot.StartsAtUtc < dayEndUtc)
            .OrderBy(slot => slot.StartsAtUtc)
            .ToListAsync(cancellationToken);
    }

    private bool IsWithinWindow(DateOnly dateUtc) =>
        dateUtc >= Today && dateUtc <= LastBookableDate;

    private static bool IsDuplicateSlot(DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && sql.Number is UniqueIndexViolation or UniqueConstraintViolation;
}
