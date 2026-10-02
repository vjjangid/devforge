using DevForge.Application.Abstractions;
using DevForge.Domain.Builds;
using DevForge.Domain.Deployments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevForge.Application.Pipeline;

/// <summary>
/// Drives a claimed deployment through the pipeline, persisting every transition.
/// Owns the state machine; delegates the work of each stage to <see cref="IStageExecutor"/>.
/// </summary>
public sealed class DeploymentRunner(
    IAppDbContext db,
    IStageExecutor executor,
    TimeProvider clock,
    ILogger<DeploymentRunner> logger)
{
    private const string WorkerStoppedMessage = "The worker shut down before the deployment finished.";

    /// <param name="deploymentId">A deployment already claimed (status Running) by this worker.</param>
    public async Task RunAsync(Guid deploymentId, CancellationToken cancellationToken)
    {
        // Loaded without the caller's token: once claimed, the deployment must always reach a terminal state.
        var deployment = await db.Deployments
            .Include(d => d.Application)
            .FirstAsync(d => d.Id == deploymentId, CancellationToken.None);

        var log = new DeploymentLogWriter(db, clock, deployment);
        Build? build = null;

        try
        {
            await log.InfoAsync($"Deployment {deployment.Version} started", cancellationToken);

            foreach (var stage in DeploymentPipeline.Stages)
            {
                deployment.EnterStage(stage);
                if (stage == DeploymentStage.Building)
                {
                    build = Build.Start(deployment.ApplicationId, clock.GetUtcNow());
                    db.Builds.Add(build);
                    deployment.AttachBuild(build);
                }

                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Deployment {DeploymentId} entered stage {Stage}", deployment.Id, stage);

                var outcome = await executor.ExecuteAsync(CreateContext(deployment, stage, log), cancellationToken);

                if (outcome.CommitSha is { } commitSha)
                {
                    deployment.RecordCommit(commitSha);
                }

                if (outcome.Url is { } url)
                {
                    deployment.RecordUrl(url);
                }

                if (stage == DeploymentStage.Building)
                {
                    build!.Succeed(outcome.ArtifactReference, clock.GetUtcNow());
                }
            }

            deployment.Succeed(clock.GetUtcNow());
            await log.InfoAsync("Deployment completed successfully", cancellationToken);

            logger.LogInformation("Deployment {DeploymentId} succeeded", deployment.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await FailAsync(deployment, build, log, WorkerStoppedMessage);
            throw;
        }
        catch (StageFailedException exception)
        {
            await FailAsync(deployment, build, log, exception.Message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Deployment {DeploymentId} failed unexpectedly", deployment.Id);
            await FailAsync(deployment, build, log, $"An unexpected error occurred during {DescribeStage(deployment)}.");
        }
    }

    private static string DescribeStage(Deployment deployment) =>
        deployment.CurrentStage?.ToString().ToLowerInvariant() ?? "startup";

    private static StageExecutionContext CreateContext(Deployment deployment, DeploymentStage stage, IDeploymentLogWriter log) =>
        new(
            deployment.Id,
            deployment.ApplicationId,
            deployment.Application.Name,
            deployment.Application.RepositoryUrl.Value,
            deployment.Application.Branch,
            deployment.Application.Runtime,
            deployment.Version,
            stage,
            deployment.SimulateFailure,
            log);

    private async Task FailAsync(Deployment deployment, Build? build, DeploymentLogWriter log, string errorMessage)
    {
        var now = clock.GetUtcNow();

        if (build is { Status: BuildStatus.Running })
        {
            build.Fail(errorMessage, now);
        }

        deployment.Fail(errorMessage, now);

        // Not cancellable: the failure must be recorded even while the host is shutting down.
        await log.ErrorAsync($"Deployment failed: {errorMessage}", CancellationToken.None);

        logger.LogWarning(
            "Deployment {DeploymentId} failed during {Stage}: {ErrorMessage}",
            deployment.Id,
            deployment.CurrentStage,
            errorMessage);
    }
}
