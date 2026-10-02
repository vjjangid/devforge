using DevForge.Application.Pipeline;
using DevForge.Domain.Applications;
using DevForge.Domain.Deployments;
using Microsoft.Extensions.Options;

namespace DevForge.Infrastructure.Execution;

/// <summary>
/// Phase 1 executor: no code is cloned, built or deployed. Each stage waits for a configurable time
/// and writes the log lines a real pipeline would, so the rest of the system can be exercised end to end.
/// </summary>
internal sealed class SimulatedStageExecutor(IOptions<SimulationOptions> options, TimeProvider clock) : IStageExecutor
{
    public async Task<StageOutcome> ExecuteAsync(StageExecutionContext context, CancellationToken cancellationToken)
    {
        var script = ScriptFor(context);
        var shouldFail = context.SimulateFailure && context.Stage == options.Value.FailureStage;

        // The stage's delay is spread evenly across its in-progress steps.
        var stepDelay = options.Value.StageDelay / script.Steps.Count;

        foreach (var step in script.Steps)
        {
            await context.Log.InfoAsync(step, cancellationToken);
            await Task.Delay(stepDelay, clock, cancellationToken);

            if (shouldFail)
            {
                throw new StageFailedException(script.SimulatedFailure);
            }
        }

        await context.Log.InfoAsync(script.Completed, cancellationToken);
        return StageOutcome.None;
    }

    private static StageScript ScriptFor(StageExecutionContext context) =>
        context.Stage switch
        {
            DeploymentStage.Preparing => new StageScript(
                [
                    $"Preparing application {context.ApplicationName}",
                    $"Resolving {context.RepositoryUrl} at branch {context.Branch}",
                ],
                $"Workspace ready for {RuntimeCatalog.DisplayNameOf(context.Runtime)}",
                "Simulated failure: the repository could not be prepared."),

            DeploymentStage.Building => new StageScript(
                ["Build started", "Restoring dependencies", "Compiling sources"],
                "Build completed",
                "Simulated failure: the build did not compile."),

            DeploymentStage.Testing => new StageScript(
                ["Tests started", "Running test suite"],
                "Tests passed",
                "Simulated failure: the test suite reported failing tests."),

            DeploymentStage.Deploying => new StageScript(
                [$"Rolling out {context.Version}", "Waiting for health checks"],
                "Health checks passed",
                "Simulated failure: the new version did not become healthy."),

            _ => throw new ArgumentOutOfRangeException(nameof(context), context.Stage, "Unknown deployment stage."),
        };

    private sealed record StageScript(IReadOnlyList<string> Steps, string Completed, string SimulatedFailure);
}
