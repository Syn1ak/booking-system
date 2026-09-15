using FluentValidation;

namespace BookingSystem.Api.Common;

/// <summary>
/// Runs the registered validator for a slice's request before its handler. Applied per
/// endpoint rather than globally so a slice without a validator costs nothing.
/// </summary>
public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
    where TRequest : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (context.Arguments.OfType<TRequest>().FirstOrDefault() is not { } request)
        {
            return await next(context);
        }

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        return result.IsValid
            ? await next(context)
            : Results.ValidationProblem(result.ToDictionary());
    }
}
