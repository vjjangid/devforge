using DevForge.Application.Applications;
using DevForge.Application.Deployments;
using DevForge.Application.Pipeline;
using DevForge.Domain.Deployments;
using DevForge.Infrastructure.Execution;
using DevForge.Infrastructure.Execution.Processes;
using DevForge.IntegrationTests.Execution;
using DevForge.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevForge.IntegrationTests.Worker;

/// <summary>
/// The whole worker path in Docker mode against a real database, with git, docker and the health
/// check faked so the tests do not depend on what is installed.
/// </summary>
public class DockerModeProcessingTests(DatabaseFixture database) : IClassFixture<DatabaseFixture>
{
    private const string CommitSha = "7fd1a60b01f91b314f59955a4e4d4e80d8edf11d";

    private readonly FakeDocker _docker = new();

    [Fact]
    public async Task A_deployment_runs_every_stage_and_publishes_the_application()
    {
        var processes = CreateProcesses();
        await using var host = await CreateHostAsync(processes, new FakeHealthProbe(true));

        var deploymentId = await host.QueueDeploymentAsync("Docker Mode");
        await ProcessNextAsync(host);

        var deployment = await GetDeploymentAsync(host, deploymentId);
        Assert.Equal(DeploymentStatus.Succeeded, deployment.Status);
        Assert.Equal(CommitSha, deployment.CommitSha);
        Assert.StartsWith("devforge/docker-mode-", deployment.ImageReference);
        Assert.EndsWith(":v1", deployment.ImageReference);
        Assert.Equal("http://localhost:49200", deployment.Url);

        var application = await GetApplicationAsync(host, deployment.ApplicationId);
        Assert.Equal(ApplicationStatus.Running, application.Status);
        Assert.Equal("http://localhost:49200", application.Url);

        var messages = (await GetLogsAsync(host, deploymentId)).Select(log => log.Message).ToList();
        Assert.Contains("$ git --version", messages);
        Assert.Contains("Checked out commit 7fd1a60", messages);
        Assert.Contains($"Built image {deployment.ImageReference}", messages);
        Assert.Contains("Tests passed", messages);
        Assert.Contains("v1 is live at http://localhost:49200/", messages);
        Assert.Equal("Deployment completed successfully", messages[^1]);
    }

    [Fact]
    public async Task A_failed_redeployment_leaves_the_previous_version_as_the_live_one()
    {
        // First deployment answers its health check; the second never does.
        var probe = new FakeHealthProbe(true, false);
        await using var host = await CreateHostAsync(CreateProcesses(), probe);

        var first = await host.QueueDeploymentAsync("Docker Redeploy");
        await ProcessNextAsync(host);
        var applicationId = (await GetDeploymentAsync(host, first)).ApplicationId;

        _docker.PublishedPort = "127.0.0.1:49300";
        var second = await host.InScopeAsync(services => services.GetRequiredService<DeploymentService>()
            .CreateAsync(applicationId, new CreateDeploymentRequest(), CancellationToken.None));
        await ProcessNextAsync(host);

        var failed = await GetDeploymentAsync(host, second.Id);
        Assert.Equal(DeploymentStatus.Failed, failed.Status);
        Assert.Equal(DeploymentStage.Deploying, failed.CurrentStage);
        Assert.Null(failed.Url);

        // The application reports the failure but still points at the version that is actually serving.
        var application = await GetApplicationAsync(host, applicationId);
        Assert.Equal(ApplicationStatus.Failed, application.Status);
        Assert.Equal("http://localhost:49200", application.Url);
        Assert.Equal("http://localhost:49200", (await GetDeploymentAsync(host, first)).Url);
    }

    [Fact]
    public async Task A_newer_successful_deployment_takes_over_the_url()
    {
        await using var host = await CreateHostAsync(CreateProcesses(), new FakeHealthProbe(true));

        var first = await host.QueueDeploymentAsync("Docker Replace");
        await ProcessNextAsync(host);
        var applicationId = (await GetDeploymentAsync(host, first)).ApplicationId;

        _docker.PublishedPort = "127.0.0.1:49300";
        var second = await host.InScopeAsync(services => services.GetRequiredService<DeploymentService>()
            .CreateAsync(applicationId, new CreateDeploymentRequest(), CancellationToken.None));
        await ProcessNextAsync(host);

        Assert.Equal("http://localhost:49300", (await GetDeploymentAsync(host, second.Id)).Url);
        Assert.Null((await GetDeploymentAsync(host, first)).Url);
        Assert.Equal("http://localhost:49300", (await GetApplicationAsync(host, applicationId)).Url);
    }

    [Fact]
    public async Task Failing_tests_stop_the_pipeline_before_anything_is_started()
    {
        _docker.TestExitCode = 1;
        var processes = CreateProcesses();
        await using var host = await CreateHostAsync(processes, new FakeHealthProbe(true));

        var deploymentId = await host.QueueDeploymentAsync("Docker Failing Tests");
        await ProcessNextAsync(host);

        var deployment = await GetDeploymentAsync(host, deploymentId);
        Assert.Equal(DeploymentStatus.Failed, deployment.Status);
        Assert.Equal(DeploymentStage.Testing, deployment.CurrentStage);
        Assert.Contains("The tests failed", deployment.ErrorMessage);
        Assert.NotNull(deployment.ImageReference);
        Assert.DoesNotContain(processes.Commands, command => command.Arguments is ["run", ..]);
    }

    private FakeProcessRunner CreateProcesses() =>
        new FakeProcessRunner()
            .On("docker", _docker.Handle)
            .On("git", command => command.Arguments switch
            {
                ["--version"] => new FakeProcessOutcome(0, ["git version 2.47.0"]),
                [.., "rev-parse", "HEAD"] => new FakeProcessOutcome(0, [CommitSha]),
                ["clone", .., var workspace] => FakeClone(workspace),
                _ => new FakeProcessOutcome(0, []),
            });

    private Task<WorkerTestHost> CreateHostAsync(FakeProcessRunner processes, FakeHealthProbe probe) =>
        WorkerTestHost.CreateAsync(
            database.ConnectionString,
            ExecutionMode.Docker,
            services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IProcessRunner>(processes));
                services.Replace(ServiceDescriptor.Singleton<IHealthProbe>(probe));
            });

    /// <summary>What a real clone leaves behind that the later stages look for.</summary>
    private static FakeProcessOutcome FakeClone(string workspace)
    {
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "Dockerfile"), "FROM sdk AS build\nFROM build AS test\nFROM runtime\n");
        return new FakeProcessOutcome(0, [$"Cloning into '{workspace}'..."]);
    }

    private static Task<bool> ProcessNextAsync(WorkerTestHost host) =>
        host.InScopeAsync(services => services.GetRequiredService<DeploymentProcessor>().ProcessNextAsync("test-worker", CancellationToken.None));

    private static Task<DeploymentDto> GetDeploymentAsync(WorkerTestHost host, Guid id) =>
        host.InScopeAsync(services => services.GetRequiredService<DeploymentService>().GetAsync(id, CancellationToken.None));

    private static Task<ApplicationDto> GetApplicationAsync(WorkerTestHost host, Guid id) =>
        host.InScopeAsync(services => services.GetRequiredService<ApplicationService>().GetAsync(id, CancellationToken.None));

    private static Task<IReadOnlyList<DeploymentLogDto>> GetLogsAsync(WorkerTestHost host, Guid id) =>
        host.InScopeAsync(services => services.GetRequiredService<DeploymentService>().GetLogsAsync(id, afterId: null, CancellationToken.None));
}
