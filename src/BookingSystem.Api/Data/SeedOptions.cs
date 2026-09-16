using System.ComponentModel.DataAnnotations;

namespace BookingSystem.Api.Data;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>
    /// Accounts to create if they do not already exist. Empty by default, so an environment
    /// that configures nothing - production, unless it opts in - seeds no accounts at all.
    /// </summary>
    public List<SeedUser> Users { get; init; } = [];

    /// <summary>
    /// Rooms to create if no room of that name exists. Like users, empty by default.
    /// </summary>
    public List<SeedRoom> Rooms { get; init; } = [];
}

public sealed class SeedRoom
{
    [Required]
    public string Name { get; init; } = string.Empty;

    public TimeOnly OpensAtUtc { get; init; }

    public TimeOnly ClosesAtUtc { get; init; }

    public int SlotLengthMinutes { get; init; }
}

public sealed class SeedUser
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    [Required]
    public string Role { get; init; } = string.Empty;
}
