using System.Net;
using System.Net.Http.Json;
using BookingSystem.Api.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookingSystem.IntegrationTests.Fixtures;

namespace BookingSystem.IntegrationTests.Features.Auth;

[Collection(ApiCollection.Name)]
public sealed class RegisterTests(ApiFactory factory)
{
    [Fact]
    public async Task Register_WithValidRequest_Returns201()
    {
        var response = await factory.CreateClient().RegisterAsync(TestData.NewEmail());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithValidRequest_AssignsUserRole()
    {
        var client = factory.CreateClient();
        var email = TestData.NewEmail();
        await client.RegisterAsync(email);

        var login = await client.LoginSucceedsAsync(email);

        Assert.Equal(new[] { Roles.User }, login.Roles);
    }

    [Fact]
    public async Task Register_WithValidRequest_NeverGrantsAdmin()
    {
        var client = factory.CreateClient();
        var email = TestData.NewEmail();
        await client.RegisterAsync(email);

        var login = await client.LoginSucceedsAsync(email);

        Assert.DoesNotContain(Roles.Admin, login.Roles);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400()
    {
        var client = factory.CreateClient();
        var email = TestData.NewEmail();
        await client.RegisterAsync(email);

        var response = await client.RegisterAsync(email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", TestData.Password)]
    [InlineData("valid@example.com", "short")]
    [InlineData("", TestData.Password)]
    public async Task Register_WithInvalidRequest_Returns400(string email, string password)
    {
        var response = await factory.CreateClient().RegisterAsync(email, password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithInvalidRequest_DescribesTheOffendingFields()
    {
        var response = await factory.CreateClient().RegisterAsync("not-an-email", "short");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("Email", problem.Errors.Keys);
        Assert.Contains("Password", problem.Errors.Keys);
    }
}
