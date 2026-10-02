using DevForge.Infrastructure.Execution.Processes;

namespace DevForge.IntegrationTests.Execution;

/// <summary>
/// Stands in for real git and docker. Records every command and answers from a script keyed by
/// program name; programs without a script succeed silently.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Dictionary<string, Func<ProcessCommand, FakeProcessOutcome>> _scripts = [];

    public List<ProcessCommand> Commands { get; } = [];

    public FakeProcessRunner On(string fileName, int exitCode = 0, params string[] output) =>
        On(fileName, _ => new FakeProcessOutcome(exitCode, output));

    public FakeProcessRunner On(string fileName, Func<ProcessCommand, FakeProcessOutcome> script)
    {
        _scripts[fileName] = script;
        return this;
    }

    public FakeProcessRunner Throws(string fileName, Func<ProcessCommand, Exception> exception) =>
        On(fileName, command => throw exception(command));

    public async Task<ProcessResult> RunAsync(ProcessCommand command, ProcessOutputHandler onOutput, CancellationToken cancellationToken)
    {
        Commands.Add(command);

        var outcome = _scripts.TryGetValue(command.FileName, out var script)
            ? script(command)
            : new FakeProcessOutcome(ExitCode: 0, Output: []);

        foreach (var line in outcome.Output)
        {
            await onOutput(new ProcessOutputLine(ProcessOutputStream.StandardOutput, line), cancellationToken);
        }

        return new ProcessResult(outcome.ExitCode, TimeSpan.Zero);
    }
}

internal sealed record FakeProcessOutcome(int ExitCode, IReadOnlyList<string> Output);
