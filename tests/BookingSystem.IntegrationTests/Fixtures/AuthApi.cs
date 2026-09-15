using System.Net.Http.Json;
using BookingSystem.Api.Features.Auth;

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
}
