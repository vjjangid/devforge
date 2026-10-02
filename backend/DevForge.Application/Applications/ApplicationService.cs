using DevForge.Application.Abstractions;
using DevForge.Application.Common;
using DevForge.Domain.Applications;
using DevForge.Domain.Deployments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DevForge.Application.Applications;

public sealed class ApplicationService(IAppDbContext db, TimeProvider clock, ILogger<ApplicationService> logger)
{
    private const string ResourceName = "Application";

    public async Task<IReadOnlyList<ApplicationDto>> ListAsync(CancellationToken cancellationToken)
    {
        var snapshots = await db.Applications
            .AsNoTracking()
            .OrderBy(application => application.Name)
            .WithLatestDeployment()
            .ToListAsync(cancellationToken);

        return [.. snapshots.Select(snapshot => snapshot.ToDto())];
    }

    public async Task<ApplicationDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var snapshot = await db.Applications
            .AsNoTracking()
            .Where(application => application.Id == id)
            .WithLatestDeployment()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(ResourceName, id);

        return snapshot.ToDto();
    }

    public async Task<ApplicationDto> CreateAsync(CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        var application = App.Create(
            request.Name,
            request.RepositoryUrl,
            request.Branch,
            request.Runtime,
            request.Description,
            clock.GetUtcNow());

        await EnsureNameIsAvailableAsync(application, cancellationToken);

        db.Applications.Add(application);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created application {ApplicationId} ({ApplicationName})", application.Id, application.Name);

        return new ApplicationSnapshot(application, LatestDeployment: null).ToDto();
    }

    public async Task<ApplicationDto> UpdateAsync(Guid id, UpdateApplicationRequest request, CancellationToken cancellationToken)
    {
        var application = await db.Applications.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException(ResourceName, id);

        application.UpdateDetails(request.Name, request.RepositoryUrl, request.Branch, request.Description, clock.GetUtcNow());
        await EnsureNameIsAvailableAsync(application, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated application {ApplicationId}", application.Id);

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Deletes the application together with its deployments, builds and logs (database cascade).
    /// Refused while a deployment is queued or running so a worker never loses the row it is processing.
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var application = await db.Applications.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw new NotFoundException(ResourceName, id);

        var hasActiveDeployment = await db.Deployments.AnyAsync(
            deployment => deployment.ApplicationId == id && Deployment.ActiveStatuses.Contains(deployment.Status),
            cancellationToken);

        if (hasActiveDeployment)
        {
            throw new ConflictException(
                $"Application '{application.Name}' has a deployment in progress and cannot be deleted until it finishes.");
        }

        db.Applications.Remove(application);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted application {ApplicationId} ({ApplicationName})", application.Id, application.Name);
    }

    private async Task EnsureNameIsAvailableAsync(App application, CancellationToken cancellationToken)
    {
        var nameTaken = await db.Applications.AnyAsync(
            other => other.Name == application.Name && other.Id != application.Id,
            cancellationToken);

        if (nameTaken)
        {
            throw new ConflictException($"An application named '{application.Name}' already exists.");
        }
    }
}
