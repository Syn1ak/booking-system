namespace BookingSystem.Api.Authorization;

/// <summary>
/// Policy names describe a capability, never a role. The rule behind each is registered once
/// in <see cref="AuthorizationServiceCollectionExtensions"/>, so changing who may do
/// something is one edit there rather than a search across every endpoint that allows it.
/// </summary>
public static class Policies
{
    public const string CanManageRooms = "CanManageRooms";
    public const string CanViewAllBookings = "CanViewAllBookings";
}
