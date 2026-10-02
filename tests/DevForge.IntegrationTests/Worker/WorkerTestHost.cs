using DevForge.Application;
using DevForge.Application.Common;
using DevForge.Application.Deployments;
using DevForge.Application.Applications;
using DevForge.Infrastructure;
using DevForge.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevForge.IntegrationTests.Worker;

/// <summary>
/// The same service graph the Worker host builds (pipeline, queue, simulated executor) plus the
/// API-side services needed to set up data, wired to a test database with no simulated delay.
/// </summary>
internal sealed class WorkerTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private WorkerTestHost(ServiceProvider provider) => _provider = provider;

    public static async Task<WorkerTestHost> CreateAsync(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DevForge"] = connectionString,
                ["Simulation:StageDelay"] = "00:00:00",
                ["Simulation:FailureStage"] = "Testing",
                ["Features:FailureSimulation"] = "true",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        services.AddApplication();
        services.AddDeploymentPipeline();
        services.AddInfrastructure(configuration);
        services.AddSimulatedStageExecution(configuration);
        services.Configure<FeatureOptions>(configuration.GetSection(FeatureOptions.SectionName));

        var provider = services.BuildServiceProvider(validateScopes: true);
        await provider.MigrateDatabaseAsync();
        return new WorkerTestHost(provider);
    }

    /// <summary>Runs <paramref name="action"/> in its own DI scope, like one worker iteration or one HTTP request.</summary>
    public async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public async Task<Guid> QueueDeploymentAsync(string applicationName, bool simulateFailure = false)
    {
        var application = await InScopeAsync(services => services.GetRequiredService<ApplicationService>().CreateAsync(
            new CreateApplicationRequest
            {
                Name = applicationName,
                RepositoryUrl = "github.com/vijay/todo-api",
                Branch = "main",
                Runtime = "dotnet-10",
            },
            CancellationToken.None));

        var deployment = await InScopeAsync(services => services.GetRequiredService<DeploymentService>().CreateAsync(
            application.Id,
            new CreateDeploymentRequest { SimulateFailure = simulateFailure },
            CancellationToken.None));

        return deployment.Id;
    }

    public ValueTask DisposeAsync() => _provider.DisposeAsync();
}
