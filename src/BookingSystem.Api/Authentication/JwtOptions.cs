using System.ComponentModel.DataAnnotations;

namespace BookingSystem.Api.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 needs at least 256 bits of key material. Validated here so a short key
    /// fails at startup with a clear message, rather than when the first token is signed.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 24)]
    public int LifetimeHours { get; init; } = 8;
}
