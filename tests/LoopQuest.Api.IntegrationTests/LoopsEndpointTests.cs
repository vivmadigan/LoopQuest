namespace LoopQuest.Api.IntegrationTests;

/// <summary>
/// End-to-end smoke test driven through the Aspire AppHost. It boots the whole application graph
/// (a real PostgreSQL container + the API), waits for the API to report healthy, then calls the
/// reference endpoint and asserts it returns the seeded loops.
///
/// Requires Docker to be running. The first run is slow because Aspire pulls the Postgres image.
/// (System.Net, Aspire.Hosting.Testing, etc. are global usings declared in the .csproj.)
/// </summary>
public class LoopsEndpointTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    [Fact]
    public async Task GetLoops_ReturnsOkWithSeededLibrary()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.LoopQuest_AppHost>();

        await using var app = await appHost.BuildAsync().WaitAsync(StartupTimeout);
        await app.StartAsync().WaitAsync(StartupTimeout);

        using var httpClient = app.CreateHttpClient("api");
        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("api")
            .WaitAsync(StartupTimeout);

        using var response = await httpClient.GetAsync("/api/loops");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Zermatt Marathon", body);
    }
}
