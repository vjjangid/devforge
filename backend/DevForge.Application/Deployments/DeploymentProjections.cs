using DevForge.Domain.Deployments;

namespace DevForge.Application.Deployments;

internal sealed record DeploymentRow(
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
            deployment.Status,
            deployment.CurrentStage,
            deployment.SimulateFailure,
            deployment.ErrorMessage,
            deployment.CreatedAt,
            deployment.StartedAt,
            deployment.CompletedAt));
}
