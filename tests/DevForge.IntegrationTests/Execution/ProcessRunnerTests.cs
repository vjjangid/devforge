using System.Diagnostics;
using DevForge.Infrastructure.Execution.Processes;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevForge.IntegrationTests.Execution;

/// <summary>
/// Runs real programs, so these need git and a POSIX shell (<c>sh</c>) on the PATH. No database required.
/// </summary>
public class ProcessRunnerTests
{
    private static readonly TimeSpan GenerousTimeout = TimeSpan.FromSeconds(30);

    /// <summary>A cancelled or timed-out run must return well before the program would have finished by itself.</summary>
    private static readonly TimeSpan PromptReturn = TimeSpan.FromSeconds(10);

    private readonly ProcessRunner _runner = new(TimeProvider.System, NullLogger<ProcessRunner>.Instance);

    [Fact]
    public async Task Runs_a_program_and_captures_its_output()
    {
        var (result, lines) = await RunAsync("git", "--version");

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
        var line = Assert.Single(lines);
        Assert.Equal(ProcessOutputStream.StandardOutput, line.Stream);
        Assert.StartsWith("git version", line.Text);
    }

    [Fact]
    public async Task Reports_a_non_zero_exit_code_and_tags_each_line_with_its_stream()
    {
        var (result, lines) = await RunAsync("sh", "-c", "echo to-stdout; echo to-stderr 1>&2; exit 3");

        Assert.False(result.Succeeded);
        Assert.Equal(3, result.ExitCode);
        Assert.Contains(new ProcessOutputLine(ProcessOutputStream.StandardOutput, "to-stdout"), lines);
        Assert.Contains(new ProcessOutputLine(ProcessOutputStream.StandardError, "to-stderr"), lines);
    }

    [Fact]
    public async Task Passes_arguments_literally_without_a_shell_interpreting_them()
    {
        const string hostile = "main; echo injected && $(echo also-injected) | `echo backticks`";

        var (result, lines) = await RunAsync("echo", hostile);

        Assert.True(result.Succeeded);
        Assert.Equal(hostile, Assert.Single(lines).Text);
    }

    [Fact]
    public async Task Applies_the_working_directory_and_environment_variables()
    {
        var directory = Directory.CreateTempSubdirectory("devforge-process-test-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "marker.txt"), string.Empty);

            var command = new ProcessCommand
            {
                FileName = "sh",
                Arguments = ["-c", "ls; echo $DEVFORGE_TEST_VALUE"],
                WorkingDirectory = directory.FullName,
                Environment = new Dictionary<string, string> { ["DEVFORGE_TEST_VALUE"] = "from-environment" },
                Timeout = GenerousTimeout,
            };

            var (_, lines) = await RunAsync(command);

            Assert.Equal(["marker.txt", "from-environment"], lines.Select(line => line.Text));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Delivers_lines_one_at_a_time_and_in_order_within_each_stream()
    {
        const int linesPerStream = 50;
        var script = $"i=1; while [ $i -le {linesPerStream} ]; do echo out-$i; echo err-$i 1>&2; i=$((i+1)); done";

        var inHandler = 0;
        var maxConcurrency = 0;
        var lines = new List<ProcessOutputLine>();

        await _runner.RunAsync(
            Command("sh", "-c", script),
            async (line, cancellationToken) =>
            {
                maxConcurrency = Math.Max(maxConcurrency, Interlocked.Increment(ref inHandler));
                await Task.Yield();
                lines.Add(line);
                Interlocked.Decrement(ref inHandler);
            },
            CancellationToken.None);

        Assert.Equal(1, maxConcurrency);
        Assert.Equal(
            Enumerable.Range(1, linesPerStream).Select(i => $"out-{i}"),
            lines.Where(line => line.Stream == ProcessOutputStream.StandardOutput).Select(line => line.Text));
        Assert.Equal(
            Enumerable.Range(1, linesPerStream).Select(i => $"err-{i}"),
            lines.Where(line => line.Stream == ProcessOutputStream.StandardError).Select(line => line.Text));
    }

    [Fact]
    public async Task Cancellation_kills_the_program_and_the_children_it_started()
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource();
        var stopwatch = Stopwatch.StartNew();

        // The shell starts a child and waits for it. If only the shell were killed, the child would
        // keep the output pipe open and the run would hang for the full 60 seconds.
        var run = _runner.RunAsync(
            Command("sh", "-c", "echo started; sleep 60 & wait"),
            (line, _) =>
            {
                started.TrySetResult();
                return ValueTask.CompletedTask;
            },
            cancellation.Token);

        await started.Task.WaitAsync(GenerousTimeout);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.True(stopwatch.Elapsed < PromptReturn, $"Took {stopwatch.Elapsed} to stop.");
    }

    [Fact]
    public async Task A_program_that_exceeds_its_timeout_is_killed_and_reported()
    {
        var command = new ProcessCommand
        {
            FileName = "sleep",
            Arguments = ["60"],
            Timeout = TimeSpan.FromMilliseconds(300),
        };
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.ThrowsAsync<ProcessTimedOutException>(
            () => _runner.RunAsync(command, IgnoreOutput, CancellationToken.None));

        Assert.Equal("sleep", exception.FileName);
        Assert.Equal(command.Timeout, exception.Timeout);
        Assert.True(stopwatch.Elapsed < PromptReturn, $"Took {stopwatch.Elapsed} to stop.");
    }

    [Fact]
    public async Task A_program_waiting_for_input_gets_end_of_file_instead_of_hanging()
    {
        // `cat` with no arguments copies stdin to stdout until stdin closes.
        var (result, lines) = await RunAsync("cat");

        Assert.True(result.Succeeded);
        Assert.Empty(lines);
    }

    [Fact]
    public async Task A_missing_program_is_reported_clearly()
    {
        var exception = await Assert.ThrowsAsync<ProcessStartException>(
            () => RunAsync("devforge-no-such-program"));

        Assert.Equal("devforge-no-such-program", exception.FileName);
        Assert.Contains("PATH", exception.Message);
    }

    [Fact]
    public async Task A_failing_output_handler_stops_the_program_and_surfaces_the_error()
    {
        var stopwatch = Stopwatch.StartNew();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _runner.RunAsync(
            Command("sh", "-c", "echo first; sleep 60"),
            (_, _) => throw new InvalidOperationException("handler failed"),
            CancellationToken.None));

        Assert.Equal("handler failed", exception.Message);
        Assert.True(stopwatch.Elapsed < PromptReturn, $"Took {stopwatch.Elapsed} to stop.");
    }

    [Fact]
    public void ToString_shows_the_command_as_typed()
    {
        Assert.Equal("git clone --depth 1 url", Command("git", "clone", "--depth", "1", "url").ToString());
    }

    private static ProcessCommand Command(string fileName, params string[] arguments) =>
        new() { FileName = fileName, Arguments = arguments, Timeout = GenerousTimeout };

    private static ValueTask IgnoreOutput(ProcessOutputLine line, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    private Task<(ProcessResult Result, List<ProcessOutputLine> Lines)> RunAsync(string fileName, params string[] arguments) =>
        RunAsync(Command(fileName, arguments));

    private async Task<(ProcessResult Result, List<ProcessOutputLine> Lines)> RunAsync(ProcessCommand command)
    {
        var lines = new List<ProcessOutputLine>();

        var result = await _runner.RunAsync(
            command,
            (line, _) =>
            {
                lines.Add(line);
                return ValueTask.CompletedTask;
            },
            CancellationToken.None);

        return (result, lines);
    }
}
