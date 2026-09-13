using Microsoft.AspNetCore.Identity;

namespace BookingSystem.Api.Domain;

/// <summary>
/// The application's user. Adds nothing to the framework's user today; it exists so that
/// bookings reference a type we own, and so user properties can be added later without
/// the identity schema and the domain disagreeing about which type a user is.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>;
