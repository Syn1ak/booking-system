namespace BookingSystem.IntegrationTests.Fixtures;

/// <summary>
/// Test classes marked with this collection share one <see cref="ApiFactory"/>, and
/// therefore one container. xUnit runs collections sequentially, so tests in different
/// classes cannot interfere with each other's data.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = nameof(ApiCollection);
}
