using DevForge.Domain.Common;

namespace DevForge.Domain.Builds;

/// <summary>
/// The artifact-producing step of a deployment. Kept separate from the deployment so that a later
/// phase can deploy an existing build again (rollback, promotion) without rebuilding.
/// </summary>
public sealed class Build
{
    public const int ArtifactReferenceMaxLength = 500;
    public const int ErrorMessageMaxLength = 2000;

    private Build()
    {
    }

    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public BuildStatus Status { get; private set; }

    /// <summary>Where the build output lives, e.g. a container image reference.</summary>
    public string? ArtifactReference { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static Build Start(Guid applicationId, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(now),
            ApplicationId = applicationId,
            Status = BuildStatus.Running,
            StartedAt = now,
        };

    public void Succeed(string? artifactReference, DateTimeOffset now)
    {
        EnsureRunning(nameof(Succeed));
        Status = BuildStatus.Succeeded;
        ArtifactReference = artifactReference;
        CompletedAt = now;
    }

    public void Fail(string errorMessage, DateTimeOffset now)
    {
        EnsureRunning(nameof(Fail));
        Status = BuildStatus.Failed;
        ErrorMessage = errorMessage.Length > ErrorMessageMaxLength ? errorMessage[..ErrorMessageMaxLength] : errorMessage;
        CompletedAt = now;
    }

    private void EnsureRunning(string operation)
    {
        if (Status != BuildStatus.Running)
        {
            throw new InvalidStateTransitionException($"Build {Id} is {Status}; {operation} requires it to be Running.");
        }
    }
}
