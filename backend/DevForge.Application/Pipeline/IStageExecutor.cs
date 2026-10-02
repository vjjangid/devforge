namespace DevForge.Application.Pipeline;

/// <summary>
/// Does the actual work of one pipeline stage. This is the seam for future phases: the Phase 1
/// implementation simulates work, later ones can shell out to Docker, create Kubernetes Jobs, etc.
/// State transitions and persistence stay in <see cref="DeploymentRunner"/>.
/// </summary>
public interface IStageExecutor
{
    /// <exception cref="StageFailedException">The stage ran and failed; the message is shown to the user.</exception>
    Task<StageOutcome> ExecuteAsync(StageExecutionContext context, CancellationToken cancellationToken);
}
