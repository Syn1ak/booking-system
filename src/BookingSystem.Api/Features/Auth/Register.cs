using BookingSystem.Api.Authorization;
using BookingSystem.Api.Common;
using BookingSystem.Api.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace BookingSystem.Api.Features.Auth;

public sealed class Register : IEndpoint
{
    public sealed record Request(string Email, string Password);

    public sealed record Response(Guid UserId, string Email);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Email).NotEmpty().EmailAddress();
            RuleFor(request => request.Password).NotEmpty().MinimumLength(8);
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/register", Handle)
           .AllowAnonymous()
           .AddEndpointFilter<ValidationFilter<Request>>()
           .WithName(nameof(Register));

    private static async Task<IResult> Handle(
        Request request,
        UserManager<ApplicationUser> users)
    {
        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return Results.ValidationProblem(ToErrors(created));
        }

        // Every registration is a regular user. Administrators are seeded, never self-assigned.
        var assigned = await users.AddToRoleAsync(user, Roles.User);
        if (!assigned.Succeeded)
        {
            await users.DeleteAsync(user);
            return Results.ValidationProblem(ToErrors(assigned));
        }

        return Results.Created((string?)null, new Response(user.Id, request.Email));
    }

    private static Dictionary<string, string[]> ToErrors(IdentityResult result) =>
        result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(group => group.Key, group => group.Select(e => e.Description).ToArray());
}
