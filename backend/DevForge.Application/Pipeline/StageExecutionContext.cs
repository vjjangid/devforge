using DevForge.Domain.Deployments;

namespace DevForge.Application.Pipeline;

/// <summary>Everything an executor needs to run a stage, without access to the entities themselves.</summary>
public sealed record StageExecutionContext(
    Guid DeploymentId,
    Guid ApplicationId,
    string ApplicationName,
    string RepositoryUrl,
    string Branch,
    string Runtime,
    string Version,
    DeploymentStage Stage,
    bool SimulateFailure,
    IDeploymentLogWriter Log);
