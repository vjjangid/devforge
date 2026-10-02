using DevForge.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace DevForge.Application.Pipeline;

/// <summary>One unit of worker activity: claim the next queued deployment, if any, and run it to completion.</summary>
public sealed class DeploymentProcessor(
    IDeploymentQueue queue,
    DeploymentRunner runner,
    ILogger<DeploymentProcessor> logger)
{
    /// <returns><c>true</c> if a deployment was processed; <c>false</c> if the queue was empty.</returns>
    public async Task<bool> ProcessNextAsync(string workerId, CancellationToken cancellationToken)
    {
        var deploymentId = await queue.ClaimNextAsync(workerId, cancellationToken);
        if (deploymentId is null)
        {
            return false;
        }

        logger.LogInformation("Worker {WorkerId} claimed deployment {DeploymentId}", workerId, deploymentId);

        await runner.RunAsync(deploymentId.Value, cancellationToken);
        return true;
    }
}
