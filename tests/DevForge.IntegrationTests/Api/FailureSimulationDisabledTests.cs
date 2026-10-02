using System.Net;
using System.Net.Http.Json;
using DevForge.Application.Platform;
using DevForge.IntegrationTests.Infrastructure;

namespace DevForge.IntegrationTests.Api;

public class FailureSimulationDisabledTests(DatabaseFixture database) : ApiTestBase(database, failureSimulation: false)
{
    [Fact]
    public async Task Requesting_a_simulated_failure_is_rejected()
    {
        var application = await CreateApplicationAsync("No Simulation");

        var response = await Client.PostAsJsonAsync($"/api/applications/{application.Id}/deployments", new { simulateFailure = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Platform_info_reports_the_feature_as_off()
    {
        var info = await Client.GetFromJsonAsync<PlatformInfoDto>("/api/platform", ApiJson.Options);

        Assert.False(info!.Features.FailureSimulation);
        Assert.Contains(info.Runtimes, runtime => runtime.Key == "dotnet-10");
    }
}
