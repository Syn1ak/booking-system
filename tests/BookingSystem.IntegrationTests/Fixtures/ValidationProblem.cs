namespace BookingSystem.IntegrationTests.Fixtures;

/// <summary>
/// The shape of an ASP.NET Core validation response, so tests can assert which field was
/// rejected rather than only that something was.
/// </summary>
internal sealed record ValidationProblem(Dictionary<string, string[]> Errors);
