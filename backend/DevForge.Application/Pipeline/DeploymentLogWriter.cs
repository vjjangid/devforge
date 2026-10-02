using DevForge.Application.Abstractions;
using DevForge.Domain.Deployments;

namespace DevForge.Application.Pipeline;

/// <summary>Persists each line immediately so clients polling the logs endpoint see progress as it happens.</summary>
internal sealed class DeploymentLogWriter(IAppDbContext db, TimeProvider clock, Deployment deployment) : IDeploymentLogWriter
{
    public async Task WriteAsync(DeploymentLogLevel level, string message, CancellationToken cancellationToken)
    {
        db.DeploymentLogs.Add(DeploymentLog.Create(deployment.Id, level, deployment.CurrentStage, message, clock.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
    }
}
