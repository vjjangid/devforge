namespace DevForge.Infrastructure.Execution.Processes;

internal enum ProcessOutputStream
{
    StandardOutput,
    StandardError,
}

/// <summary>One line a running program wrote, and which stream it wrote it to.</summary>
internal readonly record struct ProcessOutputLine(ProcessOutputStream Stream, string Text);
