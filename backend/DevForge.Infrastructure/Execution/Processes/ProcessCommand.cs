namespace DevForge.Infrastructure.Execution.Processes;

/// <summary>A command-line program to run, described as data so it can be logged and tested.</summary>
internal sealed record ProcessCommand
{
    /// <summary>The executable, resolved through <c>PATH</c> (for example <c>git</c> or <c>docker</c>).</summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Arguments, one entry per argument. Each entry reaches the program exactly as written: no shell
    /// is involved, so values that came from users (repository URLs, branch names) cannot inject commands.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];

    /// <summary>Directory to run in. Defaults to the current process's working directory.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Environment variables to set in addition to the ones inherited from this process.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    /// <summary>How long the program may run before it is killed. Required: nothing may run unbounded.</summary>
    public required TimeSpan Timeout { get; init; }

    /// <summary>The command as a person would type it. For log messages only; never executed.</summary>
    public override string ToString() => Arguments.Count == 0 ? FileName : $"{FileName} {string.Join(' ', Arguments)}";
}
