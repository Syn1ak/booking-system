using System.Security.Claims;

namespace BookingSystem.Api.Common;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The caller's user id. Present on any authorized endpoint: the token carries it, and
    /// token validation rejects one without it.
    /// </summary>
    public static Guid UserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
