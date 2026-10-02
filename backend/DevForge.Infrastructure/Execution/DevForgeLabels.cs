using DevForge.Application.Pipeline;

namespace DevForge.Infrastructure.Execution;

/// <summary>
/// Labels put on every image and container DevForge creates, so they can be found again
/// (to replace an application's previous container, or to clean up) without keeping a separate record.
/// </summary>
internal static class DevForgeLabels
{
    public const string ApplicationId = "devforge.application-id";
    public const string DeploymentId = "devforge.deployment-id";
    public const string Version = "devforge.version";

    /// <summary>The <c>--label key=value</c> arguments identifying what a deployment produced.</summary>
    public static IEnumerable<string> ArgumentsFor(StageExecutionContext context) =>
    [
        "--label", $"{ApplicationId}={context.ApplicationId}",
        "--label", $"{DeploymentId}={context.DeploymentId}",
        "--label", $"{Version}={context.Version}",
    ];

    /// <summary>A <c>docker ps --filter</c> value matching everything that belongs to one application.</summary>
    public static string FilterForApplication(Guid applicationId) => $"label={ApplicationId}={applicationId}";
}
