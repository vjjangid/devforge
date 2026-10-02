using DevForge.Domain.Deployments;

namespace DevForge.Application.Deployments;

internal sealed record DeploymentRow(
    Guid Id,
    Guid ApplicationId,
    string ApplicationName,
    int Number,
    string Version,
    string? CommitSha,
    string? ImageReference,
    string? Url,
    bool IsLive,
    DeploymentStatus Status,
    DeploymentStage? CurrentStage,
    bool SimulateFailure,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt)
{
    public DeploymentDto ToDto() =>
        new(
            Id,
            ApplicationId,
            ApplicationName,
            Number,
            Version,
            CommitSha,
            ImageReference,
            IsLive ? Url : null,
            Status,
            CurrentStage,
            SimulateFailure,
            ErrorMessage,
            CreatedAt,
            StartedAt,
            CompletedAt,
            PipelineView.For(Status, CurrentStage));
}

internal static class DeploymentProjections
{
    public static IQueryable<DeploymentRow> ToRows(this IQueryable<Deployment> deployments) =>
        deployments.Select(deployment => new DeploymentRow(
            deployment.Id,
            deployment.ApplicationId,
            deployment.Application.Name,
            deployment.Number,
            deployment.Version,
            deployment.CommitSha,
            deployment.Build!.ArtifactReference,
            deployment.Url,
            // Live = the newest deployment of its application that succeeded.
            deployment.Status == DeploymentStatus.Succeeded
                && !deployment.Application.Deployments.Any(
                    other => other.Status == DeploymentStatus.Succeeded && other.Number > deployment.Number),
            deployment.Status,
            deployment.CurrentStage,
            deployment.SimulateFailure,
            deployment.ErrorMessage,
            deployment.CreatedAt,
            deployment.StartedAt,
            deployment.CompletedAt));
}
