using DevForge.Domain.Deployments;

namespace DevForge.Application.Deployments;

public sealed record DeploymentDto(
    Guid Id,
    Guid ApplicationId,
    string ApplicationName,
    int Number,
    string Version,
    string? CommitSha,
    DeploymentStatus Status,
    DeploymentStage? CurrentStage,
    bool SimulateFailure,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<PipelineStepDto> Pipeline);

public sealed record PipelineStepDto(string Name, PipelineStepState State);

public enum PipelineStepState
{
    Pending,
    Active,
    Completed,
    Failed,
}

public sealed record DeploymentLogDto(
    long Id,
    DateTimeOffset Timestamp,
    DeploymentLogLevel Level,
    DeploymentStage? Stage,
    string Message);

public sealed class CreateDeploymentRequest
{
    /// <summary>Development aid: ask the worker to fail this deployment at a predictable stage.</summary>
    public bool SimulateFailure { get; init; }
}
