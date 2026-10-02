namespace DevForge.Application.Pipeline;

/// <param name="ArtifactReference">Set by the build stage: where the produced artifact can be found.</param>
public sealed record StageOutcome(string? ArtifactReference = null)
{
    public static StageOutcome None { get; } = new();
}
