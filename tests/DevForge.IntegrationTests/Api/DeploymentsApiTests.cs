using System.Net;
using System.Net.Http.Json;
using DevForge.Application.Applications;
using DevForge.Application.Dashboard;
using DevForge.Application.Deployments;
using DevForge.Domain.Deployments;
using DevForge.IntegrationTests.Infrastructure;

namespace DevForge.IntegrationTests.Api;

public class DeploymentsApiTests(DatabaseFixture database) : ApiTestBase(database)
{
    [Fact]
    public async Task Create_queues_a_deployment_and_returns_its_location()
    {
        var application = await CreateApplicationAsync("Deploy Test");

        var response = await Client.PostAsJsonAsync($"/api/applications/{application.Id}/deployments", new { simulateFailure = true });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var deployment = await response.ReadAsync<DeploymentDto>();
        Assert.Equal(DeploymentStatus.Queued, deployment.Status);
        Assert.Equal(1, deployment.Number);
        Assert.Equal("v1", deployment.Version);
        Assert.True(deployment.SimulateFailure);
        Assert.Null(deployment.StartedAt);
        Assert.Equal("Deploy Test", deployment.ApplicationName);
        Assert.Equal(PipelineStepState.Active, deployment.Pipeline[0].State);
        Assert.EndsWith($"/api/deployments/{deployment.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Create_accepts_a_request_without_a_body()
    {
        var application = await CreateApplicationAsync("Deploy Without Body");

        var response = await Client.PostAsync($"/api/applications/{application.Id}/deployments", content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False((await response.ReadAsync<DeploymentDto>()).SimulateFailure);
    }

    [Fact]
    public async Task Create_returns_404_for_an_unknown_application()
    {
        var response = await Client.PostAsJsonAsync($"/api/applications/{Guid.NewGuid()}/deployments", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_409_while_another_deployment_is_in_progress()
    {
        var application = await CreateApplicationAsync("Deploy Twice");
        await CreateDeploymentAsync(application.Id);

        var response = await Client.PostAsJsonAsync($"/api/applications/{application.Id}/deployments", new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Concurrent_requests_create_exactly_one_deployment()
    {
        var application = await CreateApplicationAsync("Deploy Concurrently");

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(
            _ => Client.PostAsJsonAsync($"/api/applications/{application.Id}/deployments", new { })));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.All(
            responses.Where(response => response.StatusCode != HttpStatusCode.Created),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
    }

    [Fact]
    public async Task Get_and_list_return_the_deployment_and_update_the_application_status()
    {
        var application = await CreateApplicationAsync("Deploy Read");
        var created = await CreateDeploymentAsync(application.Id);

        var fetched = await Client.GetFromJsonAsync<DeploymentDto>($"/api/deployments/{created.Id}", ApiJson.Options);
        var history = await Client.GetFromJsonAsync<List<DeploymentDto>>($"/api/applications/{application.Id}/deployments", ApiJson.Options);
        var refreshed = await Client.GetFromJsonAsync<ApplicationDto>($"/api/applications/{application.Id}", ApiJson.Options);

        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal(created.Id, Assert.Single(history!).Id);
        Assert.Equal(ApplicationStatus.Deploying, refreshed!.Status);
        Assert.Equal(created.Id, refreshed.LastDeployment!.Id);
    }

    [Fact]
    public async Task Logs_start_with_the_queued_entry_and_support_a_cursor()
    {
        var application = await CreateApplicationAsync("Deploy Logs");
        var deployment = await CreateDeploymentAsync(application.Id);

        var logs = await Client.GetFromJsonAsync<List<DeploymentLogDto>>($"/api/deployments/{deployment.Id}/logs", ApiJson.Options);
        var entry = Assert.Single(logs!);
        Assert.Equal("Deployment v1 queued", entry.Message);

        var newer = await Client.GetFromJsonAsync<List<DeploymentLogDto>>(
            $"/api/deployments/{deployment.Id}/logs?afterId={entry.Id}", ApiJson.Options);
        Assert.Empty(newer!);
    }

    [Fact]
    public async Task Unknown_deployments_return_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/deployments/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/deployments/{Guid.NewGuid()}/logs")).StatusCode);
    }

    [Fact]
    public async Task Dashboard_summary_counts_real_data()
    {
        var before = await Client.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary", ApiJson.Options);

        var application = await CreateApplicationAsync("Dashboard Test");
        var deployment = await CreateDeploymentAsync(application.Id);

        var after = await Client.GetFromJsonAsync<DashboardSummaryDto>("/api/dashboard/summary", ApiJson.Options);

        Assert.Equal(before!.Applications + 1, after!.Applications);
        Assert.Equal(before.DeploymentsToday + 1, after.DeploymentsToday);
        Assert.Equal(application.Id, after.RecentApplications[0].Id);
        Assert.Equal(deployment.Id, after.RecentDeployments[0].Id);
    }

    [Fact]
    public async Task Health_endpoints_report_healthy()
    {
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/health/ready")).StatusCode);
    }
}
