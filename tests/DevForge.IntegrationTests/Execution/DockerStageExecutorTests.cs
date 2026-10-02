using DevForge.Application.Pipeline;
using DevForge.Domain.Deployments;
using DevForge.Infrastructure.Execution;
using DevForge.Infrastructure.Execution.Processes;

namespace DevForge.IntegrationTests.Execution;

/// <summary>Exercises the executor against a fake process runner: no git, docker, network or database needed.</summary>
public class DockerStageExecutorTests
{
    private const string CommitSha = "7fd1a60b01f91b314f59955a4e4d4e80d8edf11d";

    private static readonly ExecutionOptions Options = new()
    {
        Mode = ExecutionMode.Docker,
        WorkspaceRoot = Path.Combine(Path.GetTempPath(), "devforge-tests", "executor"),
        CommandTimeout = TimeSpan.FromMinutes(3),
    };

    private readonly FakeProcessRunner _processes = new FakeProcessRunner()
        .On("git", GitSucceeds)
        .On("docker", exitCode: 0, "Docker 27.0.3");

    private readonly RecordingLogWriter _log = new();

    [Fact]
    public async Task Preparing_checks_the_tools_clones_the_branch_and_returns_the_commit()
    {
        var deploymentId = Guid.NewGuid();
        var workspace = Options.WorkspacePathFor(deploymentId);

        var outcome = await ExecuteAsync(DeploymentStage.Preparing, deploymentId: deploymentId);

        Assert.Equal(CommitSha, outcome.CommitSha);
        Assert.Equal(
            [
                "git --version",
                "docker version --format Docker {{.Server.Version}}",
                $"git clone --depth 1 --single-branch --branch=main -- https://github.com/vijay/devforge-sample {workspace}",
                $"git -C {workspace} rev-parse HEAD",
            ],
            _processes.Commands.Select(command => command.ToString()));
    }

    [Fact]
    public async Task The_clone_cannot_prompt_and_uses_the_configured_timeout()
    {
        await ExecuteAsync(DeploymentStage.Preparing);

        var clone = _processes.Commands.Single(command => command.Arguments.Contains("clone"));
        Assert.Equal("0", clone.Environment["GIT_TERMINAL_PROMPT"]);
        Assert.Equal(Options.CommandTimeout, clone.Timeout);
    }

    [Fact]
    public async Task Preparing_logs_each_command_with_its_output()
    {
        await ExecuteAsync(DeploymentStage.Preparing);

        Assert.Equal("Checking required tools", _log.Messages[0]);
        Assert.Contains("$ git --version", _log.Messages);
        Assert.Contains("git version 2.47.0", _log.Messages);
        Assert.Contains("Docker 27.0.3", _log.Messages);
        Assert.Contains("Cloning https://github.com/vijay/devforge-sample at branch main", _log.Messages);
        Assert.Contains("Cloning into 'workspace'...", _log.Messages);
        Assert.Equal("Checked out commit 7fd1a60", _log.Messages[^1]);
    }

    [Fact]
    public async Task Blank_output_lines_are_not_written_to_the_deployment_log()
    {
        _processes.On("git", command => command.Arguments.Contains("clone")
            ? new FakeProcessOutcome(0, ["Cloning into 'workspace'...", "", "   ", "done"])
            : GitSucceeds(command));

        await ExecuteAsync(DeploymentStage.Preparing);

        Assert.Contains("done", _log.Messages);
        Assert.DoesNotContain(_log.Messages, string.IsNullOrWhiteSpace);
    }

    [Fact]
    public async Task A_workspace_left_by_an_earlier_run_is_cleared_before_cloning()
    {
        var deploymentId = Guid.NewGuid();
        var workspace = Options.WorkspacePathFor(deploymentId);
        Directory.CreateDirectory(workspace);
        await File.WriteAllTextAsync(Path.Combine(workspace, "stale.txt"), "left behind");

        await ExecuteAsync(DeploymentStage.Preparing, deploymentId: deploymentId);

        Assert.False(Directory.Exists(workspace));
    }

    [Fact]
    public async Task A_failed_clone_names_the_repository_and_branch()
    {
        _processes.On("git", command => command.Arguments.Contains("clone")
            ? new FakeProcessOutcome(128, ["fatal: Remote branch nope not found in upstream origin"])
            : GitSucceeds(command));

        var exception = await Assert.ThrowsAsync<StageFailedException>(
            () => ExecuteAsync(DeploymentStage.Preparing, branch: "nope"));

        Assert.Contains("Could not clone https://github.com/vijay/devforge-sample at branch 'nope'", exception.Message);
        Assert.Contains("exited with code 128", exception.Message);
        Assert.Contains("fatal: Remote branch nope not found in upstream origin", _log.Messages);
    }

    [Fact]
    public async Task An_unreadable_commit_id_fails_the_stage()
    {
        _processes.On("git", command => command.Arguments.Contains("rev-parse")
            ? new FakeProcessOutcome(0, ["HEAD"])
            : GitSucceeds(command));

        var exception = await Assert.ThrowsAsync<StageFailedException>(() => ExecuteAsync(DeploymentStage.Preparing));

        Assert.Contains("unexpected commit id", exception.Message);
    }

    [Fact]
    public async Task Preparing_fails_clearly_when_a_tool_is_not_installed()
    {
        _processes.Throws("docker", command => new ProcessStartException(command.FileName, new FileNotFoundException()));

        var exception = await Assert.ThrowsAsync<StageFailedException>(() => ExecuteAsync(DeploymentStage.Preparing));

        Assert.Equal("Could not start 'docker'. Is it installed and on the PATH?", exception.Message);
        Assert.DoesNotContain(_processes.Commands, command => command.Arguments.Contains("clone"));
    }

    [Fact]
    public async Task Preparing_fails_clearly_when_the_docker_daemon_is_not_running()
    {
        _processes.On("docker", exitCode: 1, "Cannot connect to the Docker daemon");

        var exception = await Assert.ThrowsAsync<StageFailedException>(() => ExecuteAsync(DeploymentStage.Preparing));

        Assert.Contains("Is the Docker daemon running?", exception.Message);
        Assert.Contains("exited with code 1", exception.Message);
        Assert.Contains("Cannot connect to the Docker daemon", _log.Messages);
    }

    [Fact]
    public async Task Preparing_fails_clearly_when_a_command_hangs()
    {
        _processes.Throws("git", command => new ProcessTimedOutException(command.FileName, command.Timeout));

        var exception = await Assert.ThrowsAsync<StageFailedException>(() => ExecuteAsync(DeploymentStage.Preparing));

        Assert.Contains("'git' did not finish within", exception.Message);
        Assert.DoesNotContain(_processes.Commands, command => command.FileName == "docker");
    }

    [Fact]
    public async Task Building_runs_docker_build_on_the_workspace_and_returns_the_image()
    {
        var deploymentId = Guid.NewGuid();
        var applicationId = Guid.Parse("01a0fdd6-11d0-7d67-9c20-317940ea4c59");
        var workspace = CreateWorkspace(deploymentId, withDockerfile: true);

        var outcome = await ExecutorTestKit.CreateExecutor(_processes, Options).ExecuteAsync(
            StageContexts.Create(DeploymentStage.Building, _log, deploymentId: deploymentId, applicationId: applicationId, applicationName: "Todo API"),
            CancellationToken.None);

        const string image = "devforge/todo-api-40ea4c59:v1";
        Assert.Equal(image, outcome.ArtifactReference);
        Assert.Null(outcome.CommitSha);

        var build = Assert.Single(_processes.Commands);
        Assert.Equal("docker", build.FileName);
        Assert.Equal(
            [
                "build", "--progress", "plain", "--tag", image,
                "--label", $"devforge.application-id={applicationId}",
                "--label", $"devforge.deployment-id={deploymentId}",
                "--label", "devforge.version=v1",
                "--file", Path.Combine(workspace, "Dockerfile"),
                workspace,
            ],
            build.Arguments);
        Assert.Equal(Options.CommandTimeout, build.Timeout);
        Assert.Equal($"Building image {image}", _log.Messages[0]);
        Assert.Equal($"Built image {image}", _log.Messages[^1]);
    }

    [Fact]
    public async Task Building_fails_without_calling_docker_when_the_repository_has_no_dockerfile()
    {
        var deploymentId = Guid.NewGuid();
        CreateWorkspace(deploymentId, withDockerfile: false);

        var exception = await Assert.ThrowsAsync<StageFailedException>(
            () => ExecuteAsync(DeploymentStage.Building, deploymentId: deploymentId));

        Assert.Contains("has no Dockerfile at its root", exception.Message);
        Assert.Empty(_processes.Commands);
    }

    [Fact]
    public async Task A_failed_build_points_at_the_build_output()
    {
        var deploymentId = Guid.NewGuid();
        CreateWorkspace(deploymentId, withDockerfile: true);
        _processes.On("docker", exitCode: 1, "#9 ERROR: process \"dotnet build\" did not complete successfully");

        var exception = await Assert.ThrowsAsync<StageFailedException>(
            () => ExecuteAsync(DeploymentStage.Building, deploymentId: deploymentId));

        Assert.Contains("The image could not be built", exception.Message);
        Assert.Contains("exited with code 1", exception.Message);
        Assert.Contains(_log.Messages, message => message.Contains("did not complete successfully", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Testing_builds_the_test_stage_of_the_dockerfile()
    {
        var deploymentId = Guid.NewGuid();
        var workspace = CreateWorkspace(deploymentId, dockerfile: "FROM sdk AS build\nFROM build AS test\nRUN dotnet test\nFROM runtime\n");

        var outcome = await ExecuteAsync(DeploymentStage.Testing, deploymentId: deploymentId);

        Assert.Same(StageOutcome.None, outcome);
        var test = Assert.Single(_processes.Commands);
        Assert.Equal(
            ["build", "--progress", "plain", "--target", "test", "--file", Path.Combine(workspace, "Dockerfile"), workspace],
            test.Arguments);
        Assert.Equal("Tests passed", _log.Messages[^1]);
    }

    [Theory]
    [InlineData("FROM scratch\n")]
    [InlineData("FROM sdk AS build\nFROM build AS testing\nFROM runtime\n")]
    [InlineData("# FROM build AS test\nFROM scratch\n")]
    public async Task Testing_is_skipped_with_a_warning_when_the_dockerfile_has_no_test_stage(string dockerfile)
    {
        var deploymentId = Guid.NewGuid();
        CreateWorkspace(deploymentId, dockerfile);

        await ExecuteAsync(DeploymentStage.Testing, deploymentId: deploymentId);

        Assert.Empty(_processes.Commands);
        Assert.Contains("no tests were run", Assert.Single(_log.Messages));
    }

    [Theory]
    [InlineData("FROM build AS test")]
    [InlineData("from build as TEST  ")]
    [InlineData("  FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0 AS test")]
    public async Task The_test_stage_is_recognised_however_it_is_written(string declaration)
    {
        var deploymentId = Guid.NewGuid();
        CreateWorkspace(deploymentId, $"FROM sdk AS build\n{declaration}\nRUN dotnet test\n");

        await ExecuteAsync(DeploymentStage.Testing, deploymentId: deploymentId);

        Assert.Single(_processes.Commands);
    }

    [Fact]
    public async Task Failing_tests_fail_the_stage()
    {
        var deploymentId = Guid.NewGuid();
        CreateWorkspace(deploymentId, "FROM build AS test\n");
        _processes.On("docker", exitCode: 1, "#17 Failed!  - Failed: 1, Passed: 6");

        var exception = await Assert.ThrowsAsync<StageFailedException>(
            () => ExecuteAsync(DeploymentStage.Testing, deploymentId: deploymentId));

        Assert.Contains("The tests failed", exception.Message);
        Assert.Contains(_log.Messages, message => message.Contains("Failed: 1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cancellation_is_not_reported_as_a_stage_failure()
    {
        _processes.Throws("git", _ => new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => ExecuteAsync(DeploymentStage.Preparing));
    }

    private static string CreateWorkspace(Guid deploymentId, bool withDockerfile) =>
        CreateWorkspace(deploymentId, withDockerfile ? "FROM scratch" : null);

    private static string CreateWorkspace(Guid deploymentId, string? dockerfile)
    {
        var workspace = Options.WorkspacePathFor(deploymentId);
        Directory.CreateDirectory(workspace);
        if (dockerfile is not null)
        {
            File.WriteAllText(Path.Combine(workspace, "Dockerfile"), dockerfile);
        }

        return workspace;
    }

    private static FakeProcessOutcome GitSucceeds(ProcessCommand command) =>
        command.Arguments switch
        {
            ["--version"] => new FakeProcessOutcome(0, ["git version 2.47.0"]),
            ["clone", ..] => new FakeProcessOutcome(0, ["Cloning into 'workspace'..."]),
            [.., "rev-parse", "HEAD"] => new FakeProcessOutcome(0, [CommitSha]),
            _ => new FakeProcessOutcome(0, []),
        };

    private Task<StageOutcome> ExecuteAsync(DeploymentStage stage, string branch = "main", Guid? deploymentId = null) =>
        ExecutorTestKit.CreateExecutor(_processes, Options)
            .ExecuteAsync(StageContexts.Create(stage, _log, branch: branch, deploymentId: deploymentId), CancellationToken.None);
}
