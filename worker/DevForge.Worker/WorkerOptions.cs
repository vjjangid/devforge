namespace DevForge.Worker;

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    /// <summary>How long to wait before checking the queue again when it was empty or unreachable.</summary>
    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Name recorded on deployments this worker claims. Leave empty to generate one from the
    /// machine name, which keeps several worker instances distinguishable.
    /// </summary>
    public string? WorkerId { get; init; }
}
