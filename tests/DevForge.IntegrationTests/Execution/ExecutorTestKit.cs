using DevForge.Infrastructure.Execution;
using DevForge.Infrastructure.Execution.Processes;

namespace DevForge.IntegrationTests.Execution;

internal static class ExecutorTestKit
{
    /// <summary>Wires an executor the way dependency injection does, around the given process runner.</summary>
    public static DockerStageExecutor CreateExecutor(IProcessRunner processes, ExecutionOptions options, IHealthProbe? probe = null)
    {
        var wrapped = Microsoft.Extensions.Options.Options.Create(options);
        var commands = new StageCommands(processes);
        var deployer = new ContainerDeployer(commands, probe ?? new FakeHealthProbe(), wrapped, TimeProvider.System);

        return new DockerStageExecutor(commands, deployer, wrapped);
    }
}

/// <summary>Answers health checks from a script; once the script runs out, the last answer repeats.</summary>
internal sealed class FakeHealthProbe(params bool[] answers) : IHealthProbe
{
    private int _calls;

    public List<Uri> Requests { get; } = [];

    public Task<bool> IsRespondingAsync(Uri url, CancellationToken cancellationToken)
    {
        Requests.Add(url);
        var answer = answers.Length == 0 || answers[Math.Min(_calls, answers.Length - 1)];
        _calls++;
        return Task.FromResult(answer);
    }
}

/// <summary>
/// A scripted Docker CLI: enough of <c>build</c>, <c>run</c>, <c>port</c>, <c>inspect</c>, <c>ps</c>,
/// <c>logs</c> and <c>rm</c> for the executor to run a whole pipeline without a daemon.
/// </summary>
internal sealed class FakeDocker
{
    public const string NewContainerId = "aaaaaaaaaaaa1111111111111111111111111111111111111111111111111111";

    public string ExposedPorts { get; set; } = """{"8080/tcp":{}}""";
    public string PublishedPort { get; set; } = "127.0.0.1:49200";
    public bool ContainerKeepsRunning { get; set; } = true;
    public int BuildExitCode { get; set; }
    public int TestExitCode { get; set; }
    public List<string> OtherContainerIds { get; } = [];
    public List<string> ContainerOutput { get; } = [];

    public FakeProcessOutcome Handle(ProcessCommand command) =>
        command.Arguments switch
        {
            ["version", ..] => Ok("Docker 27.0.3"),
            ["build", ..] when command.Arguments.Contains("--target") => new FakeProcessOutcome(TestExitCode, ["#9 Passed!  - Failed: 0, Passed: 6"]),
            ["build", ..] => new FakeProcessOutcome(BuildExitCode, ["#22 naming to docker.io/devforge/sample:v1 done"]),
            ["image", "inspect", ..] => Ok(ExposedPorts),
            ["run", ..] => Ok(NewContainerId),
            ["port", ..] => Ok(PublishedPort),
            ["inspect", ..] => Ok(ContainerKeepsRunning ? "true" : "false"),
            ["ps", ..] => Ok([NewContainerId, .. OtherContainerIds]),
            ["logs", ..] => Ok([.. ContainerOutput]),
            _ => Ok(),
        };

    private static FakeProcessOutcome Ok(params string[] output) => new(0, output);
}
