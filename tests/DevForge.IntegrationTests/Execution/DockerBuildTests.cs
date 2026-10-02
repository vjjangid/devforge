using DevForge.Application.Pipeline;
using DevForge.Domain.Deployments;
using DevForge.Infrastructure.Execution;
using DevForge.Infrastructure.Execution.Processes;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevForge.IntegrationTests.Execution;

/// <summary>
/// The Building stage with a real Docker daemon. The Dockerfiles build <c>FROM scratch</c>, so nothing
/// is pulled and the tests work offline.
/// </summary>
public sealed class DockerBuildTests : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private readonly ProcessRunner _processes = new(TimeProvider.System, NullLogger<ProcessRunner>.Instance);
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("devforge-build-test-");
    private readonly RecordingLogWriter _log = new();
    private readonly Guid _deploymentId = Guid.NewGuid();
    private readonly Guid _applicationId = Guid.NewGuid();
    private string? _image;

    private ExecutionOptions Options => new() { Mode = ExecutionMode.Docker, WorkspaceRoot = _root.FullName, CommandTimeout = Timeout };
    private string Workspace => Options.WorkspacePathFor(_deploymentId);

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(Workspace);
        return File.WriteAllTextAsync(Path.Combine(Workspace, "hello.txt"), "hello");
    }

    public async Task DisposeAsync()
    {
        if (_image is not null)
        {
            await DockerAsync("image", "rm", "--force", _image);
        }

        _root.Delete(recursive: true);
    }

    [DockerFact]
    public async Task Builds_a_labelled_image_from_the_workspace()
    {
        await File.WriteAllTextAsync(Path.Combine(Workspace, "Dockerfile"), "FROM scratch\nCOPY hello.txt /hello.txt\n");

        var outcome = await BuildAsync();

        _image = outcome.ArtifactReference;
        Assert.Equal(ImageNaming.ImageReferenceFor(_applicationId, "Build Test", "v1"), _image);

        var (exitCode, output) = await DockerAsync(
            "image", "inspect", "--format",
            "{{index .Config.Labels \"devforge.application-id\"}} {{index .Config.Labels \"devforge.deployment-id\"}} {{index .Config.Labels \"devforge.version\"}}",
            _image!);
        Assert.Equal(0, exitCode);
        Assert.Equal($"{_applicationId} {_deploymentId} v1", output.Single());
    }

    [DockerFact]
    public async Task A_dockerfile_that_cannot_build_fails_the_stage_and_logs_dockers_error()
    {
        await File.WriteAllTextAsync(Path.Combine(Workspace, "Dockerfile"), "FROM scratch\nCOPY no-such-file.txt /\n");

        var exception = await Assert.ThrowsAsync<StageFailedException>(BuildAsync);

        Assert.Contains("The image could not be built", exception.Message);
        Assert.Contains(_log.Messages, message => message.Contains("no-such-file.txt", StringComparison.Ordinal));
    }

    private Task<StageOutcome> BuildAsync() =>
        ExecutorTestKit.CreateExecutor(_processes, Options).ExecuteAsync(
            StageContexts.Create(DeploymentStage.Building, _log, deploymentId: _deploymentId, applicationId: _applicationId, applicationName: "Build Test"),
            CancellationToken.None);

    private async Task<(int ExitCode, List<string> Output)> DockerAsync(params string[] arguments)
    {
        var output = new List<string>();
        var result = await _processes.RunAsync(
            new ProcessCommand { FileName = "docker", Arguments = arguments, Timeout = Timeout },
            (line, _) =>
            {
                output.Add(line.Text);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        return (result.ExitCode, output);
    }
}
