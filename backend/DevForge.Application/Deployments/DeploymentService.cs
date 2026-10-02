using DevForge.Application.Abstractions;
using DevForge.Application.Common;
using DevForge.Domain.Common;
using DevForge.Domain.Deployments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevForge.Application.Deployments;

public sealed class DeploymentService(
    IAppDbContext db,
    TimeProvider clock,
    IOptions<FeatureOptions> features,
    ILogger<DeploymentService> logger)
{
    private const string ApplicationResource = "Application";
    private const string DeploymentResource = "Deployment";

    public async Task<IReadOnlyList<DeploymentDto>> ListForApplicationAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        await EnsureApplicationExistsAsync(applicationId, cancellationToken);

        var rows = await db.Deployments
            .AsNoTracking()
            .Where(deployment => deployment.ApplicationId == applicationId)
            .OrderByDescending(deployment => deployment.Number)
            .ToRows()
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => row.ToDto())];
    }

    public async Task<DeploymentDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Deployments
            .AsNoTracking()
            .Where(deployment => deployment.Id == id)
            .ToRows()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(DeploymentResource, id);

        return row.ToDto();
    }

    /// <param name="afterId">Return only entries newer than this log id, so polling clients fetch just the tail.</param>
    public async Task<IReadOnlyList<DeploymentLogDto>> GetLogsAsync(Guid id, long? afterId, CancellationToken cancellationToken)
    {
        if (!await db.Deployments.AnyAsync(deployment => deployment.Id == id, cancellationToken))
        {
            throw new NotFoundException(DeploymentResource, id);
        }

        return await db.DeploymentLogs
            .AsNoTracking()
            .Where(log => log.DeploymentId == id && (afterId == null || log.Id > afterId))
            .OrderBy(log => log.Id)
            .Select(log => new DeploymentLogDto(log.Id, log.Timestamp, log.Level, log.Stage, log.Message))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Records a new deployment as <see cref="DeploymentStatus.Queued"/>. Saving the row is what makes it
    /// visible to workers; the request returns immediately and processing happens out of band.
    /// </summary>
    public async Task<DeploymentDto> CreateAsync(Guid applicationId, CreateDeploymentRequest request, CancellationToken cancellationToken)
    {
        await EnsureApplicationExistsAsync(applicationId, cancellationToken);

        if (request.SimulateFailure && !features.Value.FailureSimulation)
        {
            throw new DomainValidationException(
                nameof(request.SimulateFailure),
                "Failure simulation is not enabled in this environment.");
        }

        var hasActiveDeployment = await db.Deployments.AnyAsync(
            deployment => deployment.ApplicationId == applicationId && Deployment.ActiveStatuses.Contains(deployment.Status),
            cancellationToken);

        if (hasActiveDeployment)
        {
            throw new ConflictException("This application already has a deployment in progress.");
        }

        var lastNumber = await db.Deployments
            .Where(deployment => deployment.ApplicationId == applicationId)
            .MaxAsync(deployment => (int?)deployment.Number, cancellationToken) ?? 0;

        var now = clock.GetUtcNow();
        var deployment = Deployment.Queue(applicationId, lastNumber + 1, request.SimulateFailure, now);

        db.Deployments.Add(deployment);
        db.DeploymentLogs.Add(DeploymentLog.Create(
            deployment.Id,
            DeploymentLogLevel.Info,
            stage: null,
            $"Deployment {deployment.Version} queued",
            now));

        // A concurrent request can slip past the checks above; the database's unique indexes
        // (one active deployment per application, unique number) turn that into a ConflictException.
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Queued deployment {DeploymentId} ({Version}) for application {ApplicationId}",
            deployment.Id,
            deployment.Version,
            applicationId);

        return await GetAsync(deployment.Id, cancellationToken);
    }

    private async Task EnsureApplicationExistsAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        if (!await db.Applications.AnyAsync(application => application.Id == applicationId, cancellationToken))
        {
            throw new NotFoundException(ApplicationResource, applicationId);
        }
    }
}
