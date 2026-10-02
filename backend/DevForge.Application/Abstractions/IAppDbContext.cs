using DevForge.Domain.Applications;
using DevForge.Domain.Builds;
using DevForge.Domain.Deployments;
using Microsoft.EntityFrameworkCore;

namespace DevForge.Application.Abstractions;

/// <summary>
/// The persistence surface the application layer works against. Deliberately EF Core shaped:
/// wrapping every entity in a repository would add indirection without adding behaviour.
/// </summary>
public interface IAppDbContext
{
    DbSet<App> Applications { get; }
    DbSet<Deployment> Deployments { get; }
    DbSet<Build> Builds { get; }
    DbSet<DeploymentLog> DeploymentLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
