using DevForge.Application.Pipeline;
using DevForge.Domain.Deployments;
using DevForge.Infrastructure.Execution;
using DevForge.Infrastructure.Execution.Processes;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevForge.IntegrationTests.Execution;

/// <summary>
/// The Preparing stage with real git against a repository created on disk, so no network is needed.
/// Docker is faked: these tests are about cloning, and must pass without a Docker daemon.
/// </summary>
public sealed class GitCloneTests : IAsyncLifetime
{
    private const string DefaultBranch = "main";
    private const string FeatureBranch = "feature/login";

    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(30);

    private readonly ProcessRunner _git = new(TimeProvider.System, NullLogger<ProcessRunner>.Instance);
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("devforge-clone-test-");
    private readonly RecordingLogWriter _log = new();

    private string SourceRepository => Path.Combine(_root.FullName, "source");
    private string SourceUrl => new Uri(SourceRepository).AbsoluteUri;
    private ExecutionOptions Options => new()
    {
        Mode = ExecutionMode.Docker,
        WorkspaceRoot = Path.Combine(_root.FullName, "workspaces"),
        CommandTimeout = GitTimeout,
    };

    /// <summary>Builds a repository with one commit on main and a second commit on a feature branch.</summary>
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(SourceRepository);
        await GitAsync("init", "--initial-branch", DefaultBranch);

        await File.WriteAllTextAsync(Path.Combine(SourceRepository, "README.md"), "on main");
        await GitAsync("add", ".");
        await CommitAsync("Initial commit");

        await GitAsync("switch", "--create", FeatureBranch);
        await File.WriteAllTextAsync(Path.Combine(SourceRepository, "login.txt"), "on the feature branch");
        await GitAsync("add", ".");
        await CommitAsync("Add login");

        await GitAsync("switch", DefaultBranch);
    }

    public Task DisposeAsync()
    {
        _root.Delete(recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Clones_the_requested_branch_and_reports_its_commit()
    {
        var deploymentId = Guid.NewGuid();

        var outcome = await PrepareAsync(DefaultBranch, deploymentId);

        var workspace = Options.WorkspacePathFor(deploymentId);
        Assert.Equal(await HeadOfAsync(DefaultBranch), outcome.CommitSha);
        Assert.True(File.Exists(Path.Combine(workspace, "README.md")));
        Assert.False(File.Exists(Path.Combine(workspace, "login.txt")));
        Assert.Contains(_log.Messages, message => message.StartsWith("Checked out commit ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Clones_a_branch_other_than_the_default()
    {
        var deploymentId = Guid.NewGuid();

        var outcome = await PrepareAsync(FeatureBranch, deploymentId);

        Assert.Equal(await HeadOfAsync(FeatureBranch), outcome.CommitSha);
        Assert.NotEqual(await HeadOfAsync(DefaultBranch), outcome.CommitSha);
        Assert.True(File.Exists(Path.Combine(Options.WorkspacePathFor(deploymentId), "login.txt")));
    }

    [Fact]
    public async Task Fetches_only_the_latest_commit()
    {
        var deploymentId = Guid.NewGuid();

        await PrepareAsync(FeatureBranch, deploymentId);

        var count = await CaptureAsync(Options.WorkspacePathFor(deploymentId), "rev-list", "--count", "HEAD");
        Assert.Equal("1", count);
    }

    [Fact]
    public async Task Running_again_for_the_same_deployment_replaces_the_workspace()
    {
        var deploymentId = Guid.NewGuid();
        await PrepareAsync(DefaultBranch, deploymentId);

        var outcome = await PrepareAsync(FeatureBranch, deploymentId);

        Assert.Equal(await HeadOfAsync(FeatureBranch), outcome.CommitSha);
    }

    [Fact]
    public async Task A_branch_that_does_not_exist_fails_the_stage_and_logs_gits_explanation()
    {
        var exception = await Assert.ThrowsAsync<StageFailedException>(() => PrepareAsync("no-such-branch", Guid.NewGuid()));

        Assert.Contains("at branch 'no-such-branch'", exception.Message);
        Assert.Contains(_log.Messages, message => message.Contains("no-such-branch", StringComparison.Ordinal) && message.Contains("not found", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_repository_that_does_not_exist_fails_the_stage()
    {
        var missing = new Uri(Path.Combine(_root.FullName, "missing")).AbsoluteUri;

        var exception = await Assert.ThrowsAsync<StageFailedException>(
            () => PrepareAsync(DefaultBranch, Guid.NewGuid(), repositoryUrl: missing));

        Assert.Contains($"Could not clone {missing}", exception.Message);
    }

    private Task<StageOutcome> PrepareAsync(string branch, Guid deploymentId, string? repositoryUrl = null)
    {
        var executor = ExecutorTestKit.CreateExecutor(new RealGitFakeDocker(_git), Options);

        var context = StageContexts.Create(DeploymentStage.Preparing, _log, repositoryUrl ?? SourceUrl, branch, deploymentId);
        return executor.ExecuteAsync(context, CancellationToken.None);
    }

    private Task<string> HeadOfAsync(string branch) => CaptureAsync(SourceRepository, "rev-parse", branch);

    private Task CommitAsync(string message) =>
        GitAsync("-c", "user.name=DevForge Tests", "-c", "user.email=tests@devforge.local", "commit", "--message", message);

    private Task GitAsync(params string[] arguments) => CaptureAsync(SourceRepository, arguments);

    private async Task<string> CaptureAsync(string directory, params string[] arguments)
    {
        var lines = new List<string>();
        var result = await _git.RunAsync(
            new ProcessCommand { FileName = "git", Arguments = arguments, WorkingDirectory = directory, Timeout = GitTimeout },
            (line, _) =>
            {
                lines.Add(line.Text);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(result.Succeeded, $"git {string.Join(' ', arguments)} failed:{Environment.NewLine}{string.Join(Environment.NewLine, lines)}");
        return lines.LastOrDefault() ?? string.Empty;
    }

    /// <summary>Sends git to the real runner and answers for docker, which these tests do not need.</summary>
    private sealed class RealGitFakeDocker(IProcessRunner git) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessCommand command, ProcessOutputHandler onOutput, CancellationToken cancellationToken) =>
            command.FileName == "docker"
                ? Task.FromResult(new ProcessResult(ExitCode: 0, Duration: TimeSpan.Zero))
                : git.RunAsync(command, onOutput, cancellationToken);
    }
}
