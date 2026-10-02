namespace DevForge.Infrastructure.Execution.Processes;

/// <summary>The program could not be started at all, typically because it is not installed or not on <c>PATH</c>.</summary>
internal sealed class ProcessStartException(string fileName, Exception innerException)
    : Exception($"Could not start '{fileName}'. Is it installed and on the PATH?", innerException)
{
    public string FileName { get; } = fileName;
}

/// <summary>The program ran longer than its <see cref="ProcessCommand.Timeout"/> and was killed.</summary>
internal sealed class ProcessTimedOutException(string fileName, TimeSpan timeout)
    : Exception($"'{fileName}' did not finish within {timeout} and was stopped.")
{
    public string FileName { get; } = fileName;
    public TimeSpan Timeout { get; } = timeout;
}
