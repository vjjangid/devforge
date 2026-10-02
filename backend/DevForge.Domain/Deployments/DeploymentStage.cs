namespace DevForge.Domain.Deployments;

/// <summary>Pipeline stages a running deployment moves through, in declaration order.</summary>
public enum DeploymentStage
{
    Preparing,
    Building,
    Testing,
    Deploying,
}
