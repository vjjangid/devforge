using DevForge.Application.Pipeline;
using DevForge.Infrastructure.Execution.Processes;

namespace DevForge.Infrastructure.Execution;

/// <summary>
/// Runs external commands on behalf of a pipeline stage, translating every way a command can go
/// wrong into a <see cref="StageFailedException"/> the pipeline knows how to report.
/// </summary>
internal sealed class StageCommands(IProcessRunner processes)
{
    public const string Git = "git";
    public const string Docker = "docker";

    /// <summary>
    /// Runs a command the user should see: echoes it and streams its output into the deployment log.
    /// </summary>
    /// <param name="failureMessage">Shown when the command exits with a non-zero code.</param>
    /// <returns>The lines the command printed, for callers that need to read a value from them.</returns>
    public async Task<IReadOnlyList<string>> RunAsync(
        StageExecutionContext context,
        ProcessCommand command,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        await context.Log.InfoAsync($"$ {command}", cancellationToken);

        // stderr is logged as ordinary output: git and docker write progress there, not just errors.
        var (succeeded, exitCode, output) = await ExecuteAsync(
            command,
            // docker separates build steps with blank lines; they add nothing to a stored log.
            (line, token) => string.IsNullOrWhiteSpace(line) ? ValueTask.CompletedTask : new ValueTask(context.Log.InfoAsync(line, token)),
            cancellationToken);

        if (!succeeded)
        {
            throw new StageFailedException($"{failureMessage} ('{command.FileName}' exited with code {exitCode}.)");
        }

        return output;
    }

    /// <summary>
    /// Runs a command whose output is an answer for the executor rather than something to show the
    /// user (an inspect, a lookup). Nothing is logged, and a non-zero exit code is returned, not thrown.
    /// </summary>
    public async Task<(bool Succeeded, IReadOnlyList<string> Output)> QueryAsync(
        ProcessCommand command,
        CancellationToken cancellationToken)
    {
        var (succeeded, _, output) = await ExecuteAsync(command, (_, _) => ValueTask.CompletedTask, cancellationToken);
        return (succeeded, output);
    }

    private async Task<(bool Succeeded, int ExitCode, IReadOnlyList<string> Output)> ExecuteAsync(
        ProcessCommand command,
        Func<string, CancellationToken, ValueTask> onLine,
        CancellationToken cancellationToken)
    {
        var output = new List<string>();

        try
        {
            var result = await processes.RunAsync(
                command,
                async (line, token) =>
                {
                    output.Add(line.Text);
                    await onLine(line.Text, token);
                },
                cancellationToken);

            return (result.Succeeded, result.ExitCode, output);
        }
        catch (Exception exception) when (exception is ProcessStartException or ProcessTimedOutException)
        {
            throw new StageFailedException(exception.Message);
        }
    }
}
