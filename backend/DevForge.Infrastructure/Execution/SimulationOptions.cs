using DevForge.Domain.Deployments;

namespace DevForge.Infrastructure.Execution;

public sealed class SimulationOptions
{
    public const string SectionName = "Simulation";

    /// <summary>How long each simulated pipeline stage takes.</summary>
    public TimeSpan StageDelay { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>The stage at which a deployment requested with "simulate failure" fails.</summary>
    public DeploymentStage FailureStage { get; init; } = DeploymentStage.Testing;
}
