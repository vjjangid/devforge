using DevForge.Domain.Deployments;

namespace DevForge.Application.Pipeline;

/// <summary>Appends user-visible log lines to the deployment being processed.</summary>
public interface IDeploymentLogWriter
{
    Task WriteAsync(DeploymentLogLevel level, string message, CancellationToken cancellationToken);
}

public static class DeploymentLogWriterExtensions
{
    public static Task InfoAsync(this IDeploymentLogWriter log, string message, CancellationToken cancellationToken) =>
        log.WriteAsync(DeploymentLogLevel.Info, message, cancellationToken);

    public static Task ErrorAsync(this IDeploymentLogWriter log, string message, CancellationToken cancellationToken) =>
        log.WriteAsync(DeploymentLogLevel.Error, message, cancellationToken);
}
