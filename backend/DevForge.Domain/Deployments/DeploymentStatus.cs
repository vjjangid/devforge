namespace DevForge.Domain.Deployments;

/// <summary>Lifecycle of a deployment. Where it is inside the pipeline is tracked by <see cref="DeploymentStage"/>.</summary>
public enum DeploymentStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}
