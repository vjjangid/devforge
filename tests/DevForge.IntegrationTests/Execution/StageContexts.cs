using DevForge.Application.Pipeline;
using DevForge.Domain.Deployments;

namespace DevForge.IntegrationTests.Execution;

internal static class StageContexts
{
    public static StageExecutionContext Create(
        DeploymentStage stage,
        IDeploymentLogWriter log,
        string repositoryUrl = "https://github.com/vijay/devforge-sample",
        string branch = "main",
        Guid? deploymentId = null,
        Guid? applicationId = null,
        string applicationName = "Sample") =>
        new(
            DeploymentId: deploymentId ?? Guid.NewGuid(),
            ApplicationId: applicationId ?? Guid.NewGuid(),
            ApplicationName: applicationName,
            RepositoryUrl: repositoryUrl,
            Branch: branch,
            Runtime: "dotnet-10",
            Version: "v1",
            Stage: stage,
            SimulateFailure: false,
            Log: log);
}

internal sealed class RecordingLogWriter : IDeploymentLogWriter
{
    public List<string> Messages { get; } = [];

    public Task WriteAsync(DeploymentLogLevel level, string message, CancellationToken cancellationToken)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}
