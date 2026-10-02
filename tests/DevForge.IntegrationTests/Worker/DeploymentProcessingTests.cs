using DevForge.Application.Abstractions;
using DevForge.Application.Deployments;
using DevForge.Application.Pipeline;
using DevForge.Domain.Builds;
using DevForge.Domain.Deployments;
using DevForge.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevForge.IntegrationTests.Worker;

public class DeploymentProcessingTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>, IAsyncLifetime
{
    private const string WorkerId = "test-worker";

    private WorkerTestHost _host = null!;

    public async Task InitializeAsync() => _host = await WorkerTestHost.CreateAsync(database.ConnectionString);

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Processing_returns_false_when_nothing_is_queued()
    {
        Assert.False(await ProcessNextAsync());
    }

    [Fact]
    public async Task A_queued_deployment_runs_every_stage_and_succeeds()
    {
        var deploymentId = await _host.QueueDeploymentAsync("Worker Success");

        Assert.True(await ProcessNextAsync());

        var deployment = await GetDeploymentAsync(deploymentId);
        Assert.Equal(DeploymentStatus.Succeeded, deployment.Status);
        Assert.Equal(DeploymentStage.Deploying, deployment.CurrentStage);
        Assert.NotNull(deployment.StartedAt);
        Assert.NotNull(deployment.CompletedAt);
        Assert.Null(deployment.ErrorMessage);
        Assert.All(deployment.Pipeline, step => Assert.Equal(PipelineStepState.Completed, step.State));

        var build = await GetBuildAsync(deploymentId);
        Assert.Equal(BuildStatus.Succeeded, build!.Status);
        Assert.NotNull(build.CompletedAt);
    }

    [Fact]
    public async Task Processing_writes_ordered_logs_covering_every_stage()
    {
        var deploymentId = await _host.QueueDeploymentAsync("Worker Logs");

        await ProcessNextAsync();

        var logs = await GetLogsAsync(deploymentId);
        Assert.Equal("Deployment v1 queued", logs[0].Message);
        Assert.Equal("Deployment v1 started", logs[1].Message);
        Assert.Equal("Deployment completed successfully", logs[^1].Message);
        Assert.Equal(logs.OrderBy(log => log.Id).Select(log => log.Id), logs.Select(log => log.Id));
        Assert.All(logs, log => Assert.Equal(DeploymentLogLevel.Info, log.Level));

        foreach (var stage in DeploymentPipeline.Stages)
        {
            Assert.Contains(logs, log => log.Stage == stage);
        }
    }

    [Fact]
    public async Task A_deployment_with_simulated_failure_fails_at_the_configured_stage()
    {
        var deploymentId = await _host.QueueDeploymentAsync("Worker Failure", simulateFailure: true);

        Assert.True(await ProcessNextAsync());

        var deployment = await GetDeploymentAsync(deploymentId);
        Assert.Equal(DeploymentStatus.Failed, deployment.Status);
        Assert.Equal(DeploymentStage.Testing, deployment.CurrentStage);
        Assert.Contains("Simulated failure", deployment.ErrorMessage);
        Assert.NotNull(deployment.CompletedAt);
        Assert.Equal(PipelineStepState.Failed, deployment.Pipeline.Single(step => step.Name == nameof(DeploymentStage.Testing)).State);
        Assert.Equal(PipelineStepState.Pending, deployment.Pipeline.Single(step => step.Name == nameof(DeploymentStage.Deploying)).State);

        var logs = await GetLogsAsync(deploymentId);
        Assert.Equal(DeploymentLogLevel.Error, logs[^1].Level);
        Assert.Contains(deployment.ErrorMessage!, logs[^1].Message);
        Assert.DoesNotContain(logs, log => log.Stage == DeploymentStage.Deploying);

        // The build stage ran before the failure, so its build is complete.
        Assert.Equal(BuildStatus.Succeeded, (await GetBuildAsync(deploymentId))!.Status);
    }

    [Fact]
    public async Task An_application_can_be_deployed_again_after_a_deployment_finishes()
    {
        var first = await _host.QueueDeploymentAsync("Worker Redeploy", simulateFailure: true);
        await ProcessNextAsync();

        var applicationId = (await GetDeploymentAsync(first)).ApplicationId;
        var second = await _host.InScopeAsync(services => services.GetRequiredService<DeploymentService>()
            .CreateAsync(applicationId, new CreateDeploymentRequest(), CancellationToken.None));
        await ProcessNextAsync();

        var finished = await GetDeploymentAsync(second.Id);
        Assert.Equal(2, finished.Number);
        Assert.Equal(DeploymentStatus.Succeeded, finished.Status);
    }

    [Fact]
    public async Task Deployments_are_claimed_oldest_first()
    {
        var first = await _host.QueueDeploymentAsync("Worker Order A");
        var second = await _host.QueueDeploymentAsync("Worker Order B");

        Assert.Equal(first, await ClaimAsync(WorkerId));
        Assert.Equal(second, await ClaimAsync(WorkerId));
        Assert.Null(await ClaimAsync(WorkerId));
    }

    [Fact]
    public async Task Concurrent_workers_never_claim_the_same_deployment()
    {
        const int deployments = 5;
        const int workers = 20;

        var queued = new List<Guid>();
        for (var i = 0; i < deployments; i++)
        {
            queued.Add(await _host.QueueDeploymentAsync($"Worker Race {i}"));
        }

        var claims = await Task.WhenAll(Enumerable.Range(0, workers).Select(worker => ClaimAsync($"worker-{worker}")));

        var claimed = claims.Where(id => id is not null).Select(id => id!.Value).ToList();
        Assert.Equal(deployments, claimed.Count);
        Assert.Equal(queued.Order(), claimed.Order());

        foreach (var deploymentId in queued)
        {
            Assert.Equal(DeploymentStatus.Running, (await GetDeploymentAsync(deploymentId)).Status);
        }
    }

    private Task<bool> ProcessNextAsync() =>
        _host.InScopeAsync(services => services.GetRequiredService<DeploymentProcessor>().ProcessNextAsync(WorkerId, CancellationToken.None));

    private Task<Guid?> ClaimAsync(string workerId) =>
        _host.InScopeAsync(services => services.GetRequiredService<IDeploymentQueue>().ClaimNextAsync(workerId, CancellationToken.None));

    private Task<DeploymentDto> GetDeploymentAsync(Guid id) =>
        _host.InScopeAsync(services => services.GetRequiredService<DeploymentService>().GetAsync(id, CancellationToken.None));

    private Task<IReadOnlyList<DeploymentLogDto>> GetLogsAsync(Guid id) =>
        _host.InScopeAsync(services => services.GetRequiredService<DeploymentService>().GetLogsAsync(id, afterId: null, CancellationToken.None));

    private Task<Build?> GetBuildAsync(Guid deploymentId) =>
        _host.InScopeAsync(services => services.GetRequiredService<IAppDbContext>().Deployments
            .AsNoTracking()
            .Where(deployment => deployment.Id == deploymentId)
            .Select(deployment => deployment.Build)
            .SingleOrDefaultAsync());
}
