namespace DevForge.Application.Common;

public sealed class FeatureOptions
{
    public const string SectionName = "Features";

    /// <summary>Lets a deployment request ask the worker to fail on purpose. Intended for development only.</summary>
    public bool FailureSimulation { get; init; }
}
