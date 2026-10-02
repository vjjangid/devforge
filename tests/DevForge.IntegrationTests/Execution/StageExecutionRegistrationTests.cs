using DevForge.Application.Pipeline;
using DevForge.Infrastructure;
using DevForge.Infrastructure.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevForge.IntegrationTests.Execution;

public class StageExecutionRegistrationTests
{
    [Fact]
    public void The_simulated_executor_is_the_default()
    {
        Assert.IsType<SimulatedStageExecutor>(ResolveExecutor(mode: null));
    }

    [Theory]
    [InlineData("Simulated", typeof(SimulatedStageExecutor))]
    [InlineData("Docker", typeof(DockerStageExecutor))]
    [InlineData("docker", typeof(DockerStageExecutor))]
    public void Execution_mode_selects_the_executor(string mode, Type expected)
    {
        Assert.IsType(expected, ResolveExecutor(mode));
    }

    [Theory]
    [InlineData("Kubernetes")]
    [InlineData("7")]
    public void An_unknown_mode_stops_the_host_from_starting(string mode)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => ResolveExecutor(mode));

        Assert.Contains("Execution:Mode", exception.Message);
    }

    private static IStageExecutor ResolveExecutor(string? mode)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Execution:Mode"] = mode })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddStageExecution(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        return provider.GetRequiredService<IStageExecutor>();
    }
}
