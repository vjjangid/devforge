namespace DevForge.Infrastructure.Execution;

/// <summary>How the worker carries out pipeline stages.</summary>
public enum ExecutionMode
{
    /// <summary>Stages wait and write scripted log lines. No external tools needed.</summary>
    Simulated,

    /// <summary>Stages run real commands: git and docker.</summary>
    Docker,
}

public sealed class ExecutionOptions
{
    public const string SectionName = "Execution";

    public ExecutionMode Mode { get; init; } = ExecutionMode.Simulated;

    /// <summary>
    /// Directory under which each deployment gets its own folder for the cloned source.
    /// Defaults to a <c>devforge/workspaces</c> folder in the system temp directory.
    /// </summary>
    public string? WorkspaceRoot { get; init; }

    /// <summary>The longest any single command (a clone, a build…) may run before it is stopped.</summary>
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>The path requested on a newly started application to decide whether it is up.</summary>
    public string HealthCheckPath { get; init; } = "/";

    /// <summary>How long a newly started application has to begin answering before the deployment fails.</summary>
    public TimeSpan HealthCheckTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Pause between health check attempts.</summary>
    public TimeSpan HealthCheckInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Where a deployment's source lives. Derived from the id alone, so every stage of the same
    /// deployment finds the same folder without the stages having to share state.
    /// </summary>
    public string WorkspacePathFor(Guid deploymentId)
    {
        var root = string.IsNullOrWhiteSpace(WorkspaceRoot)
            ? Path.Combine(Path.GetTempPath(), "devforge", "workspaces")
            : WorkspaceRoot;

        return Path.Combine(Path.GetFullPath(root), deploymentId.ToString("N"));
    }
}
