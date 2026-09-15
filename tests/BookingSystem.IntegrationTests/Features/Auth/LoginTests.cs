using System.Net;
using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Features.Auth;

[Collection(ApiCollection.Name)]
public sealed class LoginTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenThatExpiresInTheFuture()
    {
        var client = factory.CreateClient();
        var email = TestData.NewEmail();
        await client.RegisterAsync(email);

        var login = await client.LoginSucceedsAsync(email);

        Assert.NotEmpty(login.Token);
        Assert.True(login.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = factory.CreateClient();
        var email = TestData.NewEmail();
        await client.RegisterAsync(email);

        var response = await client.LoginAsync(email, "Wr0ng!Passw0rd");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var response = await factory.CreateClient().LoginAsync(TestData.NewEmail());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// An unknown account and a wrong password must be indistinguishable, or the endpoint
    /// can be used to discover which addresses are registered.
    /// </summary>
    [Fact]
    public async Task Login_WithUnknownEmail_IsIndistinguishableFromWrongPassword()
    {
        var client = factory.CreateClient();
        var registered = TestData.NewEmail();
        await client.RegisterAsync(registered);

        var wrongPassword = await client.LoginAsync(registered, "Wr0ng!Passw0rd");
        var unknownAccount = await client.LoginAsync(TestData.NewEmail());

        Assert.Equal(unknownAccount.StatusCode, wrongPassword.StatusCode);
        Assert.Equal(
            await unknownAccount.Content.ReadAsStringAsync(),
            await wrongPassword.Content.ReadAsStringAsync());
    }
}
