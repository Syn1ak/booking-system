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
