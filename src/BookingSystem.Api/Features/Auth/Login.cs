using BookingSystem.Api.Authentication;
using BookingSystem.Api.Common;
using BookingSystem.Api.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace BookingSystem.Api.Features.Auth;

public sealed class Login : IEndpoint
{
    public sealed record Request(string Email, string Password);

    public sealed record Response(string Token, DateTimeOffset ExpiresAt, string[] Roles);

    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(request => request.Email).NotEmpty();
            RuleFor(request => request.Password).NotEmpty();
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/api/auth/login", Handle)
           .AllowAnonymous()
           .AddEndpointFilter<ValidationFilter<Request>>()
           .WithName(nameof(Login));

    private static async Task<IResult> Handle(
        Request request,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        TokenService tokens)
    {
        var user = await users.FindByEmailAsync(request.Email);

        // An unknown email and a wrong password return the same result, so the response
        // cannot be used to discover which accounts exist.
        if (user is null)
        {
            return Results.Unauthorized();
        }

        // CheckPasswordSignInAsync, not PasswordSignInAsync: the latter issues a sign-in
        // cookie, and no cookie scheme exists here. lockoutOnFailure is what makes
        // Identity's lockout counter advance.
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return Results.Unauthorized();
        }

        var roles = await users.GetRolesAsync(user);
        var token = tokens.Create(user, roles);

        return Results.Ok(new Response(token.Token, token.ExpiresAt, [.. roles]));
    }
}
