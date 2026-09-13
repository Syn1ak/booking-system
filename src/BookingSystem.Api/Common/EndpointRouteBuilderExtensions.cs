using System.Reflection;

namespace BookingSystem.Api.Common;

public static class EndpointRouteBuilderExtensions
{
    /// <summary>
    /// Registers every <see cref="IEndpoint"/> in this assembly. Runs once at startup.
    /// </summary>
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = typeof(IEndpoint).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                           && type.IsAssignableTo(typeof(IEndpoint)));

        foreach (var endpoint in endpoints)
        {
            // A static abstract interface member cannot be called through the interface
            // without a generic type argument, so the implementing method is located via
            // the type's interface map instead.
            var map = endpoint
                .GetInterfaceMap(typeof(IEndpoint))
                .TargetMethods
                .Single(method => method.Name.EndsWith(nameof(IEndpoint.Map), StringComparison.Ordinal));

            map.Invoke(obj: null, [app]);
        }

        return app;
    }
}
