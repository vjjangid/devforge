using DevForge.Application.Applications;
using DevForge.Application.Dashboard;
using DevForge.Application.Deployments;
using DevForge.Application.Pipeline;
using DevForge.Application.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevForge.Application;

public static class DependencyInjection
{
    /// <summary>Use cases behind the HTTP API: managing applications and queueing deployments.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<ApplicationService>();
        services.AddScoped<DeploymentService>();
        services.AddScoped<DashboardService>();
        services.AddSingleton<PlatformService>();

        return services;
    }

    /// <summary>
    /// The deployment pipeline, for hosts that execute deployments. The host must also register an
    /// <see cref="IStageExecutor"/>.
    /// </summary>
    public static IServiceCollection AddDeploymentPipeline(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<DeploymentRunner>();
        services.AddScoped<DeploymentProcessor>();

        return services;
    }
}
