using System.ComponentModel.DataAnnotations;

namespace BookingSystem.Api.Features.Rooms;

public sealed class SchedulingOptions
{
    public const string SectionName = "Scheduling";

    /// <summary>
    /// How far ahead a schedule may be read: today and this many days after it. Reading a date
    /// materialises its slots, so this also bounds what generation can write.
    /// </summary>
    [Range(1, 365)]
    public int BookingWindowDays { get; init; } = 14;
}
