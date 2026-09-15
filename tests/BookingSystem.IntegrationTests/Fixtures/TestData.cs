namespace BookingSystem.IntegrationTests.Fixtures;

internal static class TestData
{
    public const string Password = "Str0ng!Passw0rd";

    /// <summary>
    /// Every test in the assembly shares one database, so any test that creates an account
    /// must use an address no other test will use. Generating one per call keeps tests
    /// independent without needing to reset the database between them.
    /// </summary>
    public static string NewEmail() => $"test-{Guid.NewGuid():N}@example.com";
}
