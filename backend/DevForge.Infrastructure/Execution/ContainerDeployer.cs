using System.Globalization;
using System.Text.Json;
using DevForge.Application.Pipeline;
using DevForge.Domain.Deployments;
using DevForge.Infrastructure.Execution.Processes;
using Microsoft.Extensions.Options;

namespace DevForge.Infrastructure.Execution;

/// <summary>
/// The Deploying stage: starts the built image as a container next to the version already running,
/// waits for it to answer, and only then removes the old one. A deployment that never becomes
/// healthy is removed again, so a bad release does not take the application down.
/// </summary>
internal sealed class ContainerDeployer(
    StageCommands commands,
    IHealthProbe probe,
    IOptions<ExecutionOptions> options,
    TimeProvider clock)
{
    private const string Docker = StageCommands.Docker;
    private const string TcpSuffix = "/tcp";

    /// <summary>
    /// Published on the loopback interface only: the application is reachable from this machine,
    /// not from the rest of the network.
    /// </summary>
    private const string PublishAddress = "127.0.0.1";
    private const string PublicHost = "localhost";

    /// <summary>How much of a failed container's own output to copy into the deployment log.</summary>
    private const string FailureLogTail = "50";

    /// <summary>Docker's own abbreviation of a container id, as shown by <c>docker ps</c>.</summary>
    private const int ShortIdLength = 12;

    private static readonly TimeSpan DockerCommandTimeout = TimeSpan.FromSeconds(60);

    public async Task<StageOutcome> DeployAsync(StageExecutionContext context, CancellationToken cancellationToken)
    {
        var image = ImageNaming.ImageReferenceFor(context.ApplicationId, context.ApplicationName, context.Version);
        var container = ImageNaming.ContainerNameFor(context.ApplicationId, context.ApplicationName, context.Version);

        var containerPort = await FindContainerPortAsync(image, cancellationToken);
        await context.Log.InfoAsync($"The image listens on port {containerPort}", cancellationToken);

        // A container with this name can only be a leftover from an interrupted run of this same deployment.
        await RemoveAsync(container, CancellationToken.None);

        var containerId = await StartAsync(context, image, container, containerPort, cancellationToken);

        try
        {
            var url = await FindPublishedUrlAsync(container, containerPort, cancellationToken);
            await WaitUntilRespondingAsync(context, container, url, cancellationToken);
            await RemovePreviousContainersAsync(context, containerId, cancellationToken);

            await context.Log.InfoAsync($"{context.Version} is live at {url}", cancellationToken);
            return new StageOutcome(Url: url.ToString().TrimEnd('/'));
        }
        catch (Exception exception) when (exception is StageFailedException or OperationCanceledException)
        {
            // Not cancellable: whatever stopped the deployment, the half-started container must not be left running.
            await RemoveAsync(container, CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Reads the port from the image's <c>EXPOSE</c> instruction, so applications declare where they
    /// listen in the same place they declare how they are built.
    /// </summary>
    private async Task<int> FindContainerPortAsync(string image, CancellationToken cancellationToken)
    {
        var (succeeded, output) = await commands.QueryAsync(
            DockerCommand("image", "inspect", "--format", "{{json .Config.ExposedPorts}}", image),
            cancellationToken);

        if (!succeeded)
        {
            throw new StageFailedException($"The image {image} was not found. Did the build stage run?");
        }

        var ports = ParseExposedTcpPorts(output.LastOrDefault());
        if (ports.Count == 0)
        {
            throw new StageFailedException(
                "The image does not declare a port, so DevForge cannot tell where the application listens. " +
                "Add an EXPOSE instruction (for example EXPOSE 8080) to the Dockerfile.");
        }

        return ports.Min();
    }

    /// <summary>Parses Docker's <c>{"8080/tcp":{}}</c>; <c>null</c> when the image exposes nothing.</summary>
    private static List<int> ParseExposedTcpPorts(string? json)
    {
        var ports = new List<int>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return ports;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ports;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.EndsWith(TcpSuffix, StringComparison.Ordinal)
                    && int.TryParse(property.Name[..^TcpSuffix.Length], NumberStyles.None, CultureInfo.InvariantCulture, out var port))
                {
                    ports.Add(port);
                }
            }
        }
        catch (JsonException)
        {
            // Unreadable output is treated the same as "no ports"; the caller explains what is needed.
        }

        return ports;
    }

    private async Task<string> StartAsync(
        StageExecutionContext context,
        string image,
        string container,
        int containerPort,
        CancellationToken cancellationToken)
    {
        await context.Log.InfoAsync($"Starting container {container}", cancellationToken);

        var output = await commands.RunAsync(
            context,
            new ProcessCommand
            {
                FileName = Docker,
                Arguments =
                [
                    "run", "--detach",
                    "--name", container,
                    // Come back after a Docker or machine restart, but stay stopped if someone stops it on purpose.
                    "--restart", "unless-stopped",
                    // An empty host port lets Docker pick a free one, so applications never fight over ports.
                    "--publish", $"{PublishAddress}::{containerPort}",
                    "--env", $"APP_VERSION={context.Version}",
                    .. DevForgeLabels.ArgumentsFor(context),
                    image,
                ],
                Timeout = DockerCommandTimeout,
            },
            failureMessage: "The container could not be started.",
            cancellationToken);

        return output.LastOrDefault()?.Trim() ?? string.Empty;
    }

    private async Task<Uri> FindPublishedUrlAsync(string container, int containerPort, CancellationToken cancellationToken)
    {
        var (succeeded, output) = await commands.QueryAsync(
            DockerCommand("port", container, $"{containerPort}{TcpSuffix}"),
            cancellationToken);

        // Prints e.g. "127.0.0.1:49153".
        var mapping = output.FirstOrDefault() ?? string.Empty;
        var separator = mapping.LastIndexOf(':');

        if (!succeeded
            || separator < 0
            || !int.TryParse(mapping[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var hostPort))
        {
            throw new StageFailedException($"The container started but Docker did not report which port it was published on ('{mapping}').");
        }

        return new UriBuilder(Uri.UriSchemeHttp, PublicHost, hostPort).Uri;
    }

    private async Task WaitUntilRespondingAsync(
        StageExecutionContext context,
        string container,
        Uri url,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var healthUrl = new Uri(url, settings.HealthCheckPath);
        var startedAt = clock.GetTimestamp();

        await context.Log.InfoAsync($"Waiting for {healthUrl} to respond", cancellationToken);

        while (true)
        {
            if (await probe.IsRespondingAsync(healthUrl, cancellationToken))
            {
                await context.Log.InfoAsync("The application is responding", cancellationToken);
                return;
            }

            // No point waiting out the timeout for a process that has already died.
            if (!await IsRunningAsync(container, cancellationToken))
            {
                await CopyContainerOutputAsync(context, container, cancellationToken);
                throw new StageFailedException("The application stopped right after starting. Its output is in the log above.");
            }

            if (clock.GetElapsedTime(startedAt) >= settings.HealthCheckTimeout)
            {
                await CopyContainerOutputAsync(context, container, cancellationToken);
                throw new StageFailedException(
                    $"The application did not respond at {healthUrl} within {settings.HealthCheckTimeout.TotalSeconds:0} seconds. " +
                    "The previous version, if any, is still running.");
            }

            await Task.Delay(settings.HealthCheckInterval, clock, cancellationToken);
        }
    }

    private async Task<bool> IsRunningAsync(string container, CancellationToken cancellationToken)
    {
        var (succeeded, output) = await commands.QueryAsync(
            DockerCommand("inspect", "--format", "{{.State.Running}}", container),
            cancellationToken);

        return succeeded && string.Equals(output.LastOrDefault()?.Trim(), bool.TrueString, StringComparison.OrdinalIgnoreCase);
    }

    private async Task CopyContainerOutputAsync(StageExecutionContext context, string container, CancellationToken cancellationToken)
    {
        var (_, output) = await commands.QueryAsync(DockerCommand("logs", "--tail", FailureLogTail, container), cancellationToken);

        await context.Log.InfoAsync($"Last output of container {container}:", cancellationToken);
        foreach (var line in output)
        {
            await context.Log.WriteAsync(DeploymentLogLevel.Warning, line, cancellationToken);
        }
    }

    /// <summary>Removes every other container of this application now that the new one is serving.</summary>
    private async Task RemovePreviousContainersAsync(StageExecutionContext context, string currentContainerId, CancellationToken cancellationToken)
    {
        var (_, containerIds) = await commands.QueryAsync(
            DockerCommand("ps", "--all", "--quiet", "--no-trunc", "--filter", DevForgeLabels.FilterForApplication(context.ApplicationId)),
            cancellationToken);

        foreach (var id in containerIds.Select(id => id.Trim()).Where(id => id.Length > 0 && id != currentContainerId))
        {
            await context.Log.InfoAsync($"Removing previous container {id[..Math.Min(ShortIdLength, id.Length)]}", cancellationToken);
            await RemoveAsync(id, cancellationToken);
        }
    }

    /// <summary>Stops and deletes a container. Succeeds quietly when there is nothing to remove.</summary>
    private Task RemoveAsync(string container, CancellationToken cancellationToken) =>
        commands.QueryAsync(DockerCommand("rm", "--force", container), cancellationToken);

    private static ProcessCommand DockerCommand(params string[] arguments) =>
        new() { FileName = Docker, Arguments = arguments, Timeout = DockerCommandTimeout };
}
