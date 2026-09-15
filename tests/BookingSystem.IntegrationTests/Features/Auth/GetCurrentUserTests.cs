using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookingSystem.Api.Authorization;
using BookingSystem.Api.Features.Auth;
using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Features.Auth;

[Collection(ApiCollection.Name)]
public sealed class GetCurrentUserTests(ApiFactory factory)
{
    private const string Endpoint = "/api/auth/me";

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithTamperedToken_Returns401()
    {
        var client = factory.CreateClient();
        var token = await client.RegisterAndGetTokenAsync(TestData.NewEmail());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token + "x");

        var response = await client.GetAsync(Endpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsCallerIdentity()
    {
        var client = factory.CreateClient();
        var email = TestData.NewEmail();
        var token = await client.RegisterAndGetTokenAsync(email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var identity = await client.GetFromJsonAsync<GetCurrentUser.Response>(Endpoint);

        Assert.NotNull(identity);
        Assert.Equal(email, identity.Email);
        Assert.NotEqual(Guid.Empty, identity.UserId);
        Assert.Equal(new[] { Roles.User }, identity.Roles);
    }
}
