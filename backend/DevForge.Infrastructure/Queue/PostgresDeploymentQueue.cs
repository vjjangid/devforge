using DevForge.Application.Abstractions;
using DevForge.Domain.Deployments;
using DevForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevForge.Infrastructure.Queue;

/// <summary>
/// Uses the <c>deployments</c> table as the queue. <c>FOR UPDATE SKIP LOCKED</c> makes concurrent workers
/// step over a row another worker is in the middle of claiming, so each deployment is handed out exactly once
/// and workers never block each other.
/// </summary>
internal sealed class PostgresDeploymentQueue(AppDbContext db, TimeProvider clock) : IDeploymentQueue
{
    public async Task<Guid?> ClaimNextAsync(string workerId, CancellationToken cancellationToken)
    {
        var queued = DeploymentStatus.Queued.ToString();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Table and column names follow the snake_case convention configured on AppDbContext.
        var candidates = await db.Deployments
            .FromSql($"""
                SELECT *
                FROM deployments
                WHERE status = {queued}
                ORDER BY created_at, id
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (candidates is not [var deployment])
        {
            return null;
        }

        deployment.Start(workerId, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return deployment.Id;
    }
}
