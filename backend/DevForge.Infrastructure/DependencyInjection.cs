using DevForge.Application.Abstractions;
using DevForge.Application.Pipeline;
using DevForge.Infrastructure.Execution;
using DevForge.Infrastructure.Persistence;
using DevForge.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
    /// Registers the simulated pipeline executor. Only hosts that run deployments need this;
    /// a later phase swaps it for a real executor without changing the pipeline.
    /// </summary>
    public static IServiceCollection AddSimulatedStageExecution(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SimulationOptions>()
            .Bind(configuration.GetSection(SimulationOptions.SectionName))
            .Validate(options => options.StageDelay >= TimeSpan.Zero, "Simulation:StageDelay must not be negative.")
            .ValidateOnStart();

        services.AddSingleton<IStageExecutor, SimulatedStageExecutor>();

        return services;
    }
}
