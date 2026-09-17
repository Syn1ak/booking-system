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

    /// <summary>A converter registered for a type covers its nullable form too.</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder builder) =>
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

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
            //
            // Filtered to active slots: a retired row keeps its start time as history, and the
            // grid that replaces it needs the same moment.
            slot.HasIndex(s => new { s.RoomId, s.StartsAtUtc })
                .IsUnique()
                .HasFilter("[RetiredAtUtc] IS NULL");

            slot.Property(s => s.Version).IsRowVersion();

            // A token too, so the update's WHERE requires the slot to have been free. The
            // rowversion alone would not: a slot already claimed when it was read has not
            // changed, so the claim would overwrite a live booking.
            slot.Property(s => s.CurrentBookingId).IsConcurrencyToken();

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

            // A backstop, not the mechanism: it catches a code path that bypasses the claim, so
            // a violation here is a bug and is never turned into a conflict. Filtered because
            // cancelling keeps the row - a slot may carry many cancelled bookings and one live
            // one.
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
