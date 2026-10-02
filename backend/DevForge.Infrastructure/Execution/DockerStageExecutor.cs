using System.Text.RegularExpressions;
using DevForge.Application.Pipeline;
using DevForge.Domain.Deployments;
using DevForge.Infrastructure.Execution.Processes;
using Microsoft.Extensions.Options;

namespace DevForge.Infrastructure.Execution;

/// <summary>
/// Runs pipeline stages for real by driving git and docker on the machine the worker runs on.
/// State transitions and persistence stay in the pipeline runner; this class only does the work.
/// </summary>
internal sealed partial class DockerStageExecutor(
    StageCommands commands,
    ContainerDeployer deployer,
    IOptions<ExecutionOptions> options) : IStageExecutor
{
    private const string Git = StageCommands.Git;
    private const string Docker = StageCommands.Docker;
    private const int ShortShaLength = 7;
    private const string DockerfileName = "Dockerfile";

    /// <summary>
    /// The Dockerfile stage that runs the project's tests. A convention, like <c>npm test</c>:
    /// building this stage must fail when a test fails.
    /// </summary>
    private const string TestStageName = "test";

    /// <summary>
    /// Without this, git asks for a username when a repository is private or does not exist,
    /// and the clone would sit there until the timeout.
    /// </summary>
    private static readonly Dictionary<string, string> NonInteractiveGit = new() { ["GIT_TERMINAL_PROMPT"] = "0" };

    /// <summary>Asking a tool for its version is instant; anything slower means it is not working.</summary>
    private static readonly TimeSpan ToolCheckTimeout = TimeSpan.FromSeconds(15);

    public Task<StageOutcome> ExecuteAsync(StageExecutionContext context, CancellationToken cancellationToken) =>
        context.Stage switch
        {
            DeploymentStage.Preparing => PrepareAsync(context, cancellationToken),
            DeploymentStage.Building => BuildAsync(context, cancellationToken),
            DeploymentStage.Testing => TestAsync(context, cancellationToken),
            DeploymentStage.Deploying => deployer.DeployAsync(context, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(context), context.Stage, "Unknown deployment stage."),
        };

    private async Task<StageOutcome> PrepareAsync(StageExecutionContext context, CancellationToken cancellationToken)
    {
        await EnsureToolsAreAvailableAsync(context, cancellationToken);

        var workspace = options.Value.WorkspacePathFor(context.DeploymentId);
        ResetWorkspace(workspace);

        await context.Log.InfoAsync($"Cloning {context.RepositoryUrl} at branch {context.Branch}", cancellationToken);

        // Only the tip of one branch is needed to build, so skip the history and the other branches.
        // `--branch=<name>` and the `--` separator keep user-supplied values from being parsed as options.
        await commands.RunAsync(
            context,
            new ProcessCommand
            {
                FileName = Git,
                Arguments =
                [
                    "clone", "--depth", "1", "--single-branch", $"--branch={context.Branch}",
                    "--", context.RepositoryUrl, workspace,
                ],
                Environment = NonInteractiveGit,
                Timeout = options.Value.CommandTimeout,
            },
            failureMessage:
                $"Could not clone {context.RepositoryUrl} at branch '{context.Branch}'. " +
                "Check that the repository is public and the branch exists.",
            cancellationToken);

        var output = await commands.RunAsync(
            context,
            new ProcessCommand
            {
                FileName = Git,
                Arguments = ["-C", workspace, "rev-parse", "HEAD"],
                Timeout = ToolCheckTimeout,
            },
            failureMessage: "The repository was cloned but its current commit could not be read.",
            cancellationToken);

        var commitSha = output.LastOrDefault()?.Trim() ?? string.Empty;
        if (commitSha.Length != Deployment.CommitShaMaxLength || !commitSha.All(char.IsAsciiHexDigit))
        {
            throw new StageFailedException($"The repository was cloned but git reported an unexpected commit id: '{commitSha}'.");
        }

        await context.Log.InfoAsync($"Checked out commit {commitSha[..ShortShaLength]}", cancellationToken);

        return new StageOutcome(CommitSha: commitSha);
    }

    /// <summary>git refuses to clone into a folder that already has content, e.g. one left by an interrupted run.</summary>
    private static void ResetWorkspace(string workspace)
    {
        try
        {
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, recursive: true);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(workspace)!);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new StageFailedException($"Could not prepare the workspace folder: {exception.Message}");
        }
    }

    private async Task<StageOutcome> BuildAsync(StageExecutionContext context, CancellationToken cancellationToken)
    {
        var workspace = options.Value.WorkspacePathFor(context.DeploymentId);
        var dockerfile = Path.Combine(workspace, DockerfileName);

        if (!File.Exists(dockerfile))
        {
            throw new StageFailedException(
                $"The repository has no {DockerfileName} at its root. DevForge needs one to know how to build the application.");
        }

        var image = ImageNaming.ImageReferenceFor(context.ApplicationId, context.ApplicationName, context.Version);
        await context.Log.InfoAsync($"Building image {image}", cancellationToken);

        await commands.RunAsync(
            context,
            new ProcessCommand
            {
                FileName = Docker,
                Arguments =
                [
                    "build",
                    // One plain line per build step; the default output redraws the terminal and is unreadable in a log.
                    "--progress", "plain",
                    "--tag", image,
                    // Labels let later phases find and clean up everything DevForge built.
                    .. DevForgeLabels.ArgumentsFor(context),
                    "--file", dockerfile,
                    workspace,
                ],
                Timeout = options.Value.CommandTimeout,
            },
            failureMessage: "The image could not be built. The build output above shows the step that failed.",
            cancellationToken);

        await context.Log.InfoAsync($"Built image {image}", cancellationToken);

        return new StageOutcome(ArtifactReference: image);
    }

    private async Task<StageOutcome> TestAsync(StageExecutionContext context, CancellationToken cancellationToken)
    {
        var workspace = options.Value.WorkspacePathFor(context.DeploymentId);
        var dockerfile = Path.Combine(workspace, DockerfileName);

        if (!await DeclaresTestStageAsync(dockerfile, cancellationToken))
        {
            await context.Log.WriteAsync(
                DeploymentLogLevel.Warning,
                $"The {DockerfileName} has no stage named '{TestStageName}', so no tests were run. " +
                $"Add one (FROM ... AS {TestStageName}) that fails when a test fails.",
                cancellationToken);

            return StageOutcome.None;
        }

        await context.Log.InfoAsync($"Running the '{TestStageName}' stage of the {DockerfileName}", cancellationToken);

        // Reuses the layers the build stage just produced, so only the test step itself runs.
        await commands.RunAsync(
            context,
            new ProcessCommand
            {
                FileName = Docker,
                Arguments = ["build", "--progress", "plain", "--target", TestStageName, "--file", dockerfile, workspace],
                Timeout = options.Value.CommandTimeout,
            },
            failureMessage: "The tests failed. The output above shows which ones.",
            cancellationToken);

        await context.Log.InfoAsync("Tests passed", cancellationToken);

        return StageOutcome.None;
    }

    /// <summary>Looks for a <c>FROM image AS test</c> line. Docker itself would fail on a missing target.</summary>
    private static async Task<bool> DeclaresTestStageAsync(string dockerfile, CancellationToken cancellationToken)
    {
        if (!File.Exists(dockerfile))
        {
            return false;
        }

        var lines = await File.ReadAllLinesAsync(dockerfile, cancellationToken);
        return lines.Any(line => TestStageDeclaration().IsMatch(line));
    }

    [GeneratedRegex(@"^\s*FROM\s+.+\s+AS\s+" + TestStageName + @"\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TestStageDeclaration();

    /// <summary>
    /// Fails fast, with a message the user can act on, when the worker's machine cannot run a real pipeline.
    /// </summary>
    private async Task EnsureToolsAreAvailableAsync(StageExecutionContext context, CancellationToken cancellationToken)
    {
        await context.Log.InfoAsync("Checking required tools", cancellationToken);

        await commands.RunAsync(
            context,
            new ProcessCommand { FileName = Git, Arguments = ["--version"], Timeout = ToolCheckTimeout },
            failureMessage: "git is installed but did not run correctly.",
            cancellationToken);

        // Asking for the *server* version proves the Docker daemon is reachable, not just that the CLI exists.
        await commands.RunAsync(
            context,
            new ProcessCommand
            {
                FileName = Docker,
                Arguments = ["version", "--format", "Docker {{.Server.Version}}"],
                Timeout = ToolCheckTimeout,
            },
            failureMessage: "Docker is installed but not responding. Is the Docker daemon running?",
            cancellationToken);
    }
}
