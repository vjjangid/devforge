namespace DevForge.Application.Pipeline;

/// <summary>What a stage produced that the pipeline needs to record. Most stages produce nothing.</summary>
/// <param name="ArtifactReference">Set by the build stage: where the produced artifact can be found.</param>
/// <param name="CommitSha">Set by the preparing stage: the exact commit that was checked out.</param>
/// <param name="Url">Set by the deploying stage: where the running application can be reached.</param>
public sealed record StageOutcome(string? ArtifactReference = null, string? CommitSha = null, string? Url = null)
{
    public static StageOutcome None { get; } = new();
}
