using DevForge.Domain.Applications;
using DevForge.Domain.Deployments;

namespace DevForge.Application.Applications;

internal sealed record ApplicationSnapshot(App Application, DeploymentSummaryDto? LatestDeployment)
{
    public ApplicationDto ToDto() =>
        new(
            Application.Id,
            Application.Name,
            Application.RepositoryUrl.Value,
            Application.RepositoryUrl.DisplayName,
            Application.Branch,
            Application.Runtime,
            RuntimeCatalog.DisplayNameOf(Application.Runtime),
            Application.Description,
            StatusFor(LatestDeployment?.Status),
            LatestDeployment,
            Application.CreatedAt,
            Application.UpdatedAt);

    private static ApplicationStatus StatusFor(DeploymentStatus? latest) =>
        latest switch
        {
            null => ApplicationStatus.NeverDeployed,
            DeploymentStatus.Queued or DeploymentStatus.Running => ApplicationStatus.Deploying,
            DeploymentStatus.Succeeded => ApplicationStatus.Running,
            // Nothing cancels deployments in Phase 1; treat it like any other unsuccessful rollout.
            DeploymentStatus.Failed or DeploymentStatus.Cancelled => ApplicationStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(latest), latest, "Unknown deployment status."),
        };
}

internal static class ApplicationProjections
{
    /// <summary>Pairs each application with its most recent deployment in a single query.</summary>
    public static IQueryable<ApplicationSnapshot> WithLatestDeployment(this IQueryable<App> applications) =>
        applications.Select(application => new ApplicationSnapshot(
            application,
            application.Deployments
                .OrderByDescending(deployment => deployment.Number)
                .Select(deployment => new DeploymentSummaryDto(
                    deployment.Id,
                    deployment.Number,
                    deployment.Version,
                    deployment.Status,
                    deployment.CreatedAt,
                    deployment.CompletedAt))
                .FirstOrDefault()));
}
