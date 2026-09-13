namespace BookingSystem.Api.Common;

/// <summary>
/// Implemented by every use case that exposes a route. Discovered by assembly scanning, so
/// adding a slice never means editing a central registration list.
/// </summary>
public interface IEndpoint
{
    static abstract void Map(IEndpointRouteBuilder app);
}
