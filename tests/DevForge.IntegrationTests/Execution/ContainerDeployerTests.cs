using DevForge.Application.Pipeline;
using DevForge.Domain.Deployments;
using DevForge.Infrastructure.Execution;

namespace DevForge.IntegrationTests.Execution;

/// <summary>The Deploying stage against a scripted Docker and a scripted health check.</summary>
public class ContainerDeployerTests
{
    private const string Image = "devforge/todo-api-40ea4c59:v2";
    private const string Container = "devforge-todo-api-40ea4c59-v2";
    private const string OldContainerId = "bbbbbbbbbbbb2222222222222222222222222222222222222222222222222222";

    private static readonly Guid ApplicationId = Guid.Parse("01a0fdd6-11d0-7d67-9c20-317940ea4c59");
    private static readonly Guid DeploymentId = Guid.Parse("01a0fdd6-2222-7d67-9c20-317940ea4c59");

    private static readonly ExecutionOptions Options = new()
    {
        Mode = ExecutionMode.Docker,
        HealthCheckPath = "/health",
        HealthCheckTimeout = TimeSpan.FromMilliseconds(40),
        HealthCheckInterval = TimeSpan.FromMilliseconds(1),
    };

    private readonly FakeDocker _docker = new();
    private readonly FakeProcessRunner _processes = new();
    private readonly RecordingLogWriter _log = new();

    public ContainerDeployerTests() => _processes.On("docker", _docker.Handle);

    private IEnumerable<string> Commands => _processes.Commands.Select(command => command.ToString());

    [Fact]
    public async Task Starts_the_image_waits_for_it_to_respond_and_returns_its_url()
    {
        var probe = new FakeHealthProbe(false, false, true);

        var outcome = await DeployAsync(probe);

        Assert.Equal("http://localhost:49200", outcome.Url);
        Assert.Equal(3, probe.Requests.Count);
        Assert.All(probe.Requests, url => Assert.Equal("http://localhost:49200/health", url.ToString()));

        var run = _processes.Commands.Single(command => command.Arguments[0] == "run");
        Assert.Equal(
            [
                "run", "--detach", "--name", Container, "--restart", "unless-stopped",
                "--publish", "127.0.0.1::8080", "--env", "APP_VERSION=v2",
                "--label", $"devforge.application-id={ApplicationId}",
                "--label", $"devforge.deployment-id={DeploymentId}",
                "--label", "devforge.version=v2",
                Image,
            ],
            run.Arguments);
        Assert.Equal("v2 is live at http://localhost:49200/", _log.Messages[^1]);
    }

    [Fact]
    public async Task Removes_the_previous_containers_only_after_the_new_one_responds()
    {
        _docker.OtherContainerIds.Add(OldContainerId);

        await DeployAsync(new FakeHealthProbe(false, true));

        var commands = Commands.ToList();
        var removeOld = commands.IndexOf($"docker rm --force {OldContainerId}");
        var lastHealthRelated = commands.FindLastIndex(command => command.StartsWith("docker inspect", StringComparison.Ordinal));
        Assert.True(removeOld > lastHealthRelated, "The old container was removed before the new one was healthy.");
        Assert.DoesNotContain($"docker rm --force {FakeDocker.NewContainerId}", commands);
        Assert.Contains("Removing previous container bbbbbbbbbbbb", _log.Messages);
    }

    [Fact]
    public async Task An_application_that_never_responds_is_removed_and_the_previous_version_is_kept()
    {
        _docker.OtherContainerIds.Add(OldContainerId);
        _docker.ContainerOutput.Add("Unhandled exception: could not bind to port");

        var exception = await Assert.ThrowsAsync<StageFailedException>(() => DeployAsync(new FakeHealthProbe(false)));

        Assert.Contains("did not respond at http://localhost:49200/health", exception.Message);
        Assert.Contains("previous version", exception.Message);
        Assert.Equal($"docker rm --force {Container}", Commands.Last());
        Assert.DoesNotContain($"docker rm --force {OldContainerId}", Commands);
        Assert.Contains("Unhandled exception: could not bind to port", _log.Messages);
    }

    [Fact]
    public async Task An_application_that_exits_fails_straight_away_instead_of_waiting_for_the_timeout()
    {
        _docker.ContainerKeepsRunning = false;
        var probe = new FakeHealthProbe(false);

        var exception = await Assert.ThrowsAsync<StageFailedException>(() => DeployAsync(probe));

        Assert.Contains("stopped right after starting", exception.Message);
        Assert.Single(probe.Requests);
        Assert.Equal($"docker rm --force {Container}", Commands.Last());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"53/udp":{}}""")]
    [InlineData("")]
    public async Task An_image_without_an_exposed_tcp_port_cannot_be_deployed(string exposedPorts)
    {
        _docker.ExposedPorts = exposedPorts;

        var exception = await Assert.ThrowsAsync<StageFailedException>(() => DeployAsync(new FakeHealthProbe()));

        Assert.Contains("Add an EXPOSE instruction", exception.Message);
        Assert.DoesNotContain(_processes.Commands, command => command.Arguments[0] == "run");
    }

    [Fact]
    public async Task The_lowest_exposed_port_is_published()
    {
        _docker.ExposedPorts = """{"9090/tcp":{},"8080/tcp":{},"53/udp":{}}""";

        await DeployAsync(new FakeHealthProbe());

        Assert.Contains(_processes.Commands, command => command.Arguments.Contains("127.0.0.1::8080"));
        Assert.Contains($"docker port {Container} 8080/tcp", Commands);
    }

    [Fact]
    public async Task A_leftover_container_with_the_same_name_is_removed_before_starting()
    {
        await DeployAsync(new FakeHealthProbe());

        var commands = Commands.ToList();
        var cleanup = commands.IndexOf($"docker rm --force {Container}");
        var run = commands.FindIndex(command => command.StartsWith("docker run", StringComparison.Ordinal));
        Assert.True(cleanup >= 0 && cleanup < run);
    }

    [Fact]
    public async Task An_unreadable_port_mapping_fails_and_cleans_up()
    {
        _docker.PublishedPort = "";

        var exception = await Assert.ThrowsAsync<StageFailedException>(() => DeployAsync(new FakeHealthProbe()));

        Assert.Contains("did not report which port", exception.Message);
        Assert.Equal($"docker rm --force {Container}", Commands.Last());
    }

    private Task<StageOutcome> DeployAsync(FakeHealthProbe probe) =>
        ExecutorTestKit.CreateExecutor(_processes, Options, probe).ExecuteAsync(
            StageContexts.Create(DeploymentStage.Deploying, _log, deploymentId: DeploymentId, applicationId: ApplicationId, applicationName: "Todo API") with { Version = "v2" },
            CancellationToken.None);
}
