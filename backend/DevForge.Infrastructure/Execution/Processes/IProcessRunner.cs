namespace DevForge.Infrastructure.Execution.Processes;

/// <summary>Handles one line of program output. Never invoked concurrently for the same run.</summary>
internal delegate ValueTask ProcessOutputHandler(ProcessOutputLine line, CancellationToken cancellationToken);

/// <summary>
/// Runs external programs. An interface so that code built on top of it (the stage executors)
/// can be tested with a fake instead of real git and docker.
/// </summary>
internal interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="command"/> to completion, passing each line it prints to
    /// <paramref name="onOutput"/> as soon as it is written.
    /// </summary>
    /// <returns>The exit code and duration. A non-zero exit code is returned, not thrown.</returns>
    /// <exception cref="ProcessStartException">The program could not be started.</exception>
    /// <exception cref="ProcessTimedOutException">The program exceeded its timeout and was killed.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled; the program and its children were killed.
    /// </exception>
    Task<ProcessResult> RunAsync(ProcessCommand command, ProcessOutputHandler onOutput, CancellationToken cancellationToken);
}
