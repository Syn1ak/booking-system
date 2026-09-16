using BookingSystem.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BookingSystem.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Slot> Slots => Set<Slot>();

    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Room>(room =>
        {
            room.Property(r => r.Name).HasMaxLength(200);
        });

        builder.Entity<Slot>(slot =>
        {
            // The guarantee that one moment in a room is one row, and the index the schedule
            // read seeks on. Without it, two concurrent first reads of the same date - or a
            // re-run after a partial failure - produce two rows for the same moment, and two
            // people then book "the same" slot legally against different rows. The booking
            // code cannot close that door; this index is what closes it.
            slot.HasIndex(s => new { s.RoomId, s.StartsAtUtc }).IsUnique();

            slot.Property(s => s.Version).IsRowVersion();

            // A token as well as a column. EF sends a token's original value in the update's
            // WHERE clause, so the write requires the slot to have been free. The rowversion
            // alone would not: it detects changes since the read, and a slot that was already
            // claimed when it was read has not changed, so the claim would overwrite a live
            // booking. See .claude/concurrency/concurrency.md.
            slot.Property(s => s.CurrentBookingId).IsConcurrencyToken();

            // NoAction because this closes a reference cycle with the booking's slot key
            // below, and nothing deletes either row.
            slot.HasOne<Booking>()
                .WithMany()
                .HasForeignKey(s => s.CurrentBookingId)
                .OnDelete(DeleteBehavior.NoAction);

            // Restrict, not the default cascade: rooms are deactivated rather than deleted, so
            // any code path that attempts a hard delete should fail loudly instead of quietly
            // taking the room's schedule - and its bookings - with it.
            slot.HasOne<Room>()
                .WithMany()
                .HasForeignKey(s => s.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Booking>(booking =>
        {
            booking.Property(b => b.Version).IsRowVersion();

            // A backstop, not the mechanism. The slot's concurrency token is what turns a lost
            // race into a conflict response; this index exists so that a code path bypassing
            // the claim fails instead of double-booking. That is also why a violation here is
            // never translated into a conflict: the caller did not lose a race, the system is
            // wrong, and it should say so.
            //
            // Filtered, because cancelling preserves the row: a slot may carry any number of
            // cancelled bookings and at most one live one.
            booking.HasIndex(b => b.SlotId)
                   .IsUnique()
                   .HasFilter("[CancelledAtUtc] IS NULL");

            booking.HasOne<Slot>()
                   .WithMany()
                   .HasForeignKey(b => b.SlotId)
                   .OnDelete(DeleteBehavior.Restrict);

            booking.HasOne<ApplicationUser>()
                   .WithMany()
                   .HasForeignKey(b => b.UserId)
                   .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
