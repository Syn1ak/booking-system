using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BookingSystem.Api.Data;

/// <summary>
/// Marks timestamps read from SQL Server as UTC. <c>datetime2</c> carries no offset, so EF
/// returns <see cref="DateTimeKind.Unspecified"/> and the API would serialise a UTC instant with
/// no trailing <c>Z</c> - which a client reads as local time. Written through unchanged.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
