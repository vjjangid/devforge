namespace DevForge.Application.Abstractions;

/// <summary>
/// The hand-off point between the API (which queues deployments) and workers (which run them).
/// In Phase 1 a queued deployment row *is* the queue entry, so there is no separate enqueue call;
/// a broker-backed implementation can replace this without touching the pipeline.
/// </summary>
public interface IDeploymentQueue
{
    /// <summary>
    /// Atomically claims the oldest queued deployment for <paramref name="workerId"/> and marks it running.
    /// Guaranteed to hand a deployment to at most one caller.
    /// </summary>
    /// <returns>The claimed deployment's id, or <c>null</c> when nothing is queued.</returns>
    Task<Guid?> ClaimNextAsync(string workerId, CancellationToken cancellationToken);
}
