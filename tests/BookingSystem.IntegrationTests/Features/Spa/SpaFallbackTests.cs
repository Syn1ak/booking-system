using System.Net;
using BookingSystem.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;

namespace BookingSystem.IntegrationTests.Features.Spa;

[Collection(ApiCollection.Name)]
public sealed class SpaFallbackTests(ApiFactory factory) : IDisposable
{
    private const string IndexHtml = "<!doctype html><title>Booking System</title>";
    private const string AssetName = "main-TEST1234.js";
    private const string AssetBody = "export const marker = 1;";

    private readonly string _webRoot = CreateWebRoot();

    [Theory]
    [InlineData("/")]
    [InlineData("/rooms")]
    [InlineData("/rooms/3f2c7a52-5f0e-4b8e-9d0a-1c2b3d4e5f60?date=2026-09-17")]
    public async Task ClientRoute_ReturnsTheSpaEntryPoint(string path)
    {
        var response = await CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(IndexHtml, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StaticAsset_IsServedItselfRatherThanTheSpa()
    {
        var response = await CreateClient().GetAsync($"/{AssetName}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AssetBody, await response.Content.ReadAsStringAsync());
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/does-not-exist")]
    [InlineData("/hub/does-not-exist")]
    public async Task UnknownApiOrHubPath_Returns404RatherThanTheSpa(string path)
    {
        var response = await CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(IndexHtml, await response.Content.ReadAsStringAsync());
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    private HttpClient CreateClient() =>
        factory.WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot)).CreateClient();

    private static string CreateWebRoot()
    {
        var path = Directory.CreateTempSubdirectory("booking-spa-").FullName;
        File.WriteAllText(Path.Combine(path, "index.html"), IndexHtml);
        File.WriteAllText(Path.Combine(path, AssetName), AssetBody);
        return path;
    }
}
