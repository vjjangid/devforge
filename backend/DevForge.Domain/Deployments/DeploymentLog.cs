namespace DevForge.Domain.Deployments;

/// <summary>One line of deployment output. Append-only.</summary>
public sealed class DeploymentLog
{
    public const int MessageMaxLength = 2000;

    private DeploymentLog()
    {
    }

    /// <summary>Database-generated and monotonically increasing, so it doubles as a paging cursor.</summary>
    public long Id { get; private set; }
    public Guid DeploymentId { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public DeploymentLogLevel Level { get; private set; }
    public DeploymentStage? Stage { get; private set; }
    public string Message { get; private set; } = null!;

    public static DeploymentLog Create(
        Guid deploymentId,
        DeploymentLogLevel level,
        DeploymentStage? stage,
        string message,
        DateTimeOffset timestamp) =>
        new()
        {
            DeploymentId = deploymentId,
            Level = level,
            Stage = stage,
            Message = message.Length > MessageMaxLength ? message[..MessageMaxLength] : message,
            Timestamp = timestamp,
        };
}
