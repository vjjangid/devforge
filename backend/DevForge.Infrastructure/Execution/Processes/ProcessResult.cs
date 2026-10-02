namespace DevForge.Infrastructure.Execution.Processes;

/// <summary>How a program ended. A non-zero exit code is a result, not an exception; the caller decides what it means.</summary>
internal sealed record ProcessResult(int ExitCode, TimeSpan Duration)
{
    public bool Succeeded => ExitCode == 0;
}
