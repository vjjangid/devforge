using DevForge.Application.Abstractions;
using DevForge.Application.Pipeline;
using DevForge.Infrastructure.Execution;
using DevForge.Infrastructure.Execution.Processes;
using DevForge.Infrastructure.Persistence;
using DevForge.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevForge.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "DevForge";

    /// <summary>Persistence and the deployment queue. Needed by every host.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. " +
                $"Set ConnectionStrings:{ConnectionStringName} (environment variable ConnectionStrings__{ConnectionStringName}).");
        }

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IDeploymentQueue, PostgresDeploymentQueue>();

        return services;
    }

    /// <summary>
    /// Registers the <see cref="IStageExecutor"/> selected by <c>Execution:Mode</c>. Only hosts that run
    /// deployments need this. The choice is made once, at startup.
    /// </summary>
    public static IServiceCollection AddStageExecution(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ExecutionOptions.SectionName);
        var execution = section.Get<ExecutionOptions>() ?? new ExecutionOptions();

        services.AddOptions<ExecutionOptions>()
            .Bind(section)
            .Validate(options => options.CommandTimeout > TimeSpan.Zero, "Execution:CommandTimeout must be positive.")
            .Validate(options => options.HealthCheckTimeout > TimeSpan.Zero, "Execution:HealthCheckTimeout must be positive.")
            .Validate(options => options.HealthCheckInterval > TimeSpan.Zero, "Execution:HealthCheckInterval must be positive.")
            .Validate(options => options.HealthCheckPath.StartsWith('/'), "Execution:HealthCheckPath must start with '/'.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);

        switch (execution.Mode)
        {
            case ExecutionMode.Simulated:
                services.AddOptions<SimulationOptions>()
                    .Bind(configuration.GetSection(SimulationOptions.SectionName))
                    .Validate(options => options.StageDelay >= TimeSpan.Zero, "Simulation:StageDelay must not be negative.")
                    .ValidateOnStart();
                services.AddSingleton<IStageExecutor, SimulatedStageExecutor>();
                break;

            case ExecutionMode.Docker:
                services.AddSingleton<IProcessRunner, ProcessRunner>();
                services.AddSingleton<IHealthProbe, HttpHealthProbe>();
                services.AddSingleton<StageCommands>();
                services.AddSingleton<ContainerDeployer>();
                services.AddSingleton<IStageExecutor, DockerStageExecutor>();
                break;

            default:
                throw new InvalidOperationException(
                    $"Execution:Mode '{execution.Mode}' is not supported. " +
                    $"Use one of: {string.Join(", ", Enum.GetNames<ExecutionMode>())}.");
        }

        return services;
    }
}
