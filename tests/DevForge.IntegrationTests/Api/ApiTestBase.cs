using System.Net;
using System.Net.Http.Json;
using DevForge.Application.Applications;
using DevForge.Application.Deployments;
using DevForge.IntegrationTests.Infrastructure;

namespace DevForge.IntegrationTests.Api;

public abstract class ApiTestBase : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly DevForgeApiFactory _factory;

    protected ApiTestBase(DatabaseFixture database, bool failureSimulation = true)
    {
        _factory = new DevForgeApiFactory(database.ConnectionString, failureSimulation);
        Client = _factory.CreateClient();
    }

    protected HttpClient Client { get; }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    protected static object NewApplication(string name) => new
    {
        name,
        repositoryUrl = "github.com/vijay/todo-api",
        branch = "main",
        runtime = "dotnet-10",
        description = "Created by a test",
    };

    protected async Task<ApplicationDto> CreateApplicationAsync(string name)
    {
        var response = await Client.PostAsJsonAsync("/api/applications", NewApplication(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<ApplicationDto>();
    }

    protected async Task<DeploymentDto> CreateDeploymentAsync(Guid applicationId, bool simulateFailure = false)
    {
        var response = await Client.PostAsJsonAsync($"/api/applications/{applicationId}/deployments", new { simulateFailure });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.ReadAsync<DeploymentDto>();
    }
}
