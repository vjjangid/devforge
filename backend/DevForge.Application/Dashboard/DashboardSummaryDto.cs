using DevForge.Application.Applications;
using DevForge.Application.Deployments;

namespace DevForge.Application.Dashboard;

public sealed record DashboardSummaryDto(
    int Applications,
    int Running,
    int DeploymentsToday,
    int FailedDeployments,
    IReadOnlyList<ApplicationDto> RecentApplications,
    IReadOnlyList<DeploymentDto> RecentDeployments);
