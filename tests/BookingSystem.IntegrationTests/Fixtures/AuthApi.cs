using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookingSystem.Api.Authorization;
using BookingSystem.Api.Domain;
using BookingSystem.Api.Features.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BookingSystem.IntegrationTests.Fixtures;

/// <summary>
/// Helpers for driving the auth endpoints. Deliberately typed against the endpoints' own
/// response records, so a change to a contract breaks compilation here rather than being
/// silently tolerated by loosely typed test code.
/// </summary>
internal static class AuthApi
{
    public static Task<HttpResponseMessage> RegisterAsync(
        this HttpClient client, string email, string password = TestData.Password) =>
        client.PostAsJsonAsync("/api/auth/register", new { email, password });

    public static Task<HttpResponseMessage> LoginAsync(
        this HttpClient client, string email, string password = TestData.Password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    public static async Task<Login.Response> LoginSucceedsAsync(this HttpClient client, string email)
    {
        var response = await client.LoginAsync(email);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<Login.Response>())!;
    }

    /// <summary>Registers a fresh account and returns a bearer token for it.</summary>
    public static async Task<string> RegisterAndGetTokenAsync(this HttpClient client, string email)
    {
        (await client.RegisterAsync(email)).EnsureSuccessStatusCode();

        return (await client.LoginSucceedsAsync(email)).Token;
    }

    /// <summary>A client carrying a bearer token for a freshly registered regular user.</summary>
    public static async Task<HttpClient> CreateUserClientAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var token = await client.RegisterAndGetTokenAsync(TestData.NewEmail());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>
    /// A client carrying a bearer token for a freshly registered administrator. The role is
    /// granted through Identity rather than the API, which never promotes anyone.
    /// </summary>
    public static async Task<HttpClient> CreateAdminClientAsync(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = TestData.NewEmail();
        (await client.RegisterAsync(email)).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await users.AddToRoleAsync((await users.FindByEmailAsync(email))!, Roles.Admin);
        }

        // Minted after the grant: roles are claims in the token, not read per request.
        var token = (await client.LoginSucceedsAsync(email)).Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
