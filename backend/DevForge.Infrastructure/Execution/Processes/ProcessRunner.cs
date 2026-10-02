using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace DevForge.Infrastructure.Execution.Processes;

internal sealed class ProcessRunner(TimeProvider clock, ILogger<ProcessRunner> logger) : IProcessRunner
{
    /// <summary>
    /// Lines waiting to be handled. When the handler is slower than the program (it usually writes to a
    /// database), the buffer fills, reading pauses, and the program itself blocks on its next write.
    /// Memory stays bounded however chatty the program is.
    /// </summary>
    private const int OutputBufferSize = 1024;

    /// <summary>How long to wait for a killed process to actually disappear before giving up on it.</summary>
    private static readonly TimeSpan KillGracePeriod = TimeSpan.FromSeconds(5);

    public async Task<ProcessResult> RunAsync(
        ProcessCommand command,
        ProcessOutputHandler onOutput,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process { StartInfo = CreateStartInfo(command) };
        var startedAt = clock.GetTimestamp();

        Start(process, command);
        logger.LogDebug("Started process {ProcessId}: {Command}", process.Id, command);

        // One token for "stop now", whether the caller asked or the time ran out.
        using var timeout = new CancellationTokenSource(command.Timeout, clock);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        // stdout and stderr are read in parallel but funnelled into one channel, so the handler
        // sees one line at a time and never has to be thread-safe.
        var output = Channel.CreateBounded<ProcessOutputLine>(new BoundedChannelOptions(OutputBufferSize)
        {
            SingleReader = true,
        });
        var reading = ReadOutputAsync(process, output.Writer, stop.Token);

        try
        {
            await foreach (var line in output.Reader.ReadAllAsync(stop.Token))
            {
                await onOutput(line, stop.Token);
            }

            await reading;
            await process.WaitForExitAsync(stop.Token);
        }
        catch (Exception exception)
        {
            await KillAsync(process, command);
            await ObserveAsync(reading);

            if (exception is OperationCanceledException && timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new ProcessTimedOutException(command.FileName, command.Timeout);
            }

            throw;
        }

        var result = new ProcessResult(process.ExitCode, clock.GetElapsedTime(startedAt));
        logger.LogDebug(
            "Process {ProcessId} ({FileName}) exited with code {ExitCode} after {Duration}",
            process.Id,
            command.FileName,
            result.ExitCode,
            result.Duration);

        return result;
    }

    private static ProcessStartInfo CreateStartInfo(ProcessCommand command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            WorkingDirectory = command.WorkingDirectory ?? string.Empty,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        // ArgumentList, not Arguments: each entry is passed through verbatim, with no quoting to get wrong.
        foreach (var argument in command.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in command.Environment)
        {
            startInfo.Environment[name] = value;
        }

        return startInfo;
    }

    private static void Start(Process process, ProcessCommand command)
    {
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or DirectoryNotFoundException)
        {
            throw new ProcessStartException(command.FileName, exception);
        }

        // Nothing will ever be typed. Closing stdin makes a program that asks a question
        // (a password prompt, "are you sure?") fail straight away instead of waiting forever.
        process.StandardInput.Close();
    }

    private static async Task ReadOutputAsync(
        Process process,
        ChannelWriter<ProcessOutputLine> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(
                PumpAsync(process.StandardOutput, ProcessOutputStream.StandardOutput, writer, cancellationToken),
                PumpAsync(process.StandardError, ProcessOutputStream.StandardError, writer, cancellationToken));

            writer.Complete();
        }
        catch (Exception exception)
        {
            // Surfaces in the consuming loop, which then kills the process.
            writer.Complete(exception);
        }
    }

    private static async Task PumpAsync(
        StreamReader reader,
        ProcessOutputStream stream,
        ChannelWriter<ProcessOutputLine> writer,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken) is { } text)
        {
            await writer.WriteAsync(new ProcessOutputLine(stream, text), cancellationToken);
        }
    }

    /// <summary>
    /// Kills the process and everything it started. Killing only the parent would leave children
    /// (a compiler started by a build tool, for instance) running and holding the output pipes open.
    /// </summary>
    private async Task KillAsync(Process process, ProcessCommand command)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            using var grace = new CancellationTokenSource(KillGracePeriod, clock);
            await process.WaitForExitAsync(grace.Token);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception or OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not confirm that process '{FileName}' was stopped", command.FileName);
        }
    }

    /// <summary>Waits for the readers to finish after a kill; their own failure no longer matters.</summary>
    private static async Task ObserveAsync(Task reading)
    {
        try
        {
            await reading;
        }
        catch (Exception)
        {
            // Already reported through the channel, or caused by the cancellation being handled.
        }
    }
}
