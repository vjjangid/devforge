using DevForge.Application.Abstractions;
using DevForge.Application.Applications;
using DevForge.Application.Deployments;
using DevForge.Domain.Deployments;
using Microsoft.EntityFrameworkCore;

namespace DevForge.Application.Dashboard;

public sealed class DashboardService(IAppDbContext db, TimeProvider clock)
{
    private const int RecentItemCount = 5;

    /// <param name="todayStart">
    /// Start of "today" from the caller's point of view (their local midnight). Defaults to UTC midnight.
    /// </param>
    public async Task<DashboardSummaryDto> GetSummaryAsync(DateTimeOffset? todayStart, CancellationToken cancellationToken)
    {
        var since = (todayStart ?? new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero)).ToUniversalTime();

        var applications = await db.Applications.CountAsync(cancellationToken);

        // "Running" means the application's most recent deployment succeeded.
        var running = await db.Applications.CountAsync(
            application => application.Deployments
                .OrderByDescending(deployment => deployment.Number)
                .Select(deployment => (DeploymentStatus?)deployment.Status)
                .FirstOrDefault() == DeploymentStatus.Succeeded,
            cancellationToken);

        var deploymentsToday = await db.Deployments.CountAsync(deployment => deployment.CreatedAt >= since, cancellationToken);

        var failedDeployments = await db.Deployments.CountAsync(
            deployment => deployment.Status == DeploymentStatus.Failed,
            cancellationToken);

        var recentApplications = await db.Applications
            .AsNoTracking()
            .OrderByDescending(application => application.CreatedAt)
            .Take(RecentItemCount)
            .WithLatestDeployment()
            .ToListAsync(cancellationToken);

        var recentDeployments = await db.Deployments
            .AsNoTracking()
            .OrderByDescending(deployment => deployment.CreatedAt)
            .Take(RecentItemCount)
            .ToRows()
            .ToListAsync(cancellationToken);

        return new DashboardSummaryDto(
            applications,
            running,
            deploymentsToday,
            failedDeployments,
            [.. recentApplications.Select(snapshot => snapshot.ToDto())],
            [.. recentDeployments.Select(row => row.ToDto())]);
    }
}
