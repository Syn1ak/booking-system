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

            // Restrict, not the default cascade: rooms are deactivated rather than deleted, so
            // any code path that attempts a hard delete should fail loudly instead of quietly
            // taking the room's schedule - and its bookings - with it.
            slot.HasOne<Room>()
                .WithMany()
                .HasForeignKey(s => s.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
