using DevForge.Domain.Applications;
using DevForge.Domain.Builds;
using DevForge.Domain.Common;

namespace DevForge.Domain.Deployments;

public sealed class Deployment
{
    public const int VersionMaxLength = 50;
    public const int CommitShaMaxLength = 40;
    public const int ErrorMessageMaxLength = 2000;
    public const int WorkerIdMaxLength = 200;
    public const int UrlMaxLength = 500;

    /// <summary>Statuses in which a deployment is still waiting for, or being processed by, a worker.</summary>
    public static readonly DeploymentStatus[] ActiveStatuses = [DeploymentStatus.Queued, DeploymentStatus.Running];

    private readonly List<DeploymentLog> _logs = [];

    private Deployment()
    {
    }

    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public App Application { get; private set; } = null!;

    /// <summary>Sequential per application, starting at 1.</summary>
    public int Number { get; private set; }
    public string Version { get; private set; } = null!;
    public string? CommitSha { get; private set; }

    /// <summary>
    /// Where the application was published by this deployment. It only stays reachable until a
    /// later deployment replaces it.
    /// </summary>
    public string? Url { get; private set; }

    public DeploymentStatus Status { get; private set; }

    /// <summary>The stage being executed, or the stage that failed. <c>null</c> until a worker starts the pipeline.</summary>
    public DeploymentStage? CurrentStage { get; private set; }

    public bool SimulateFailure { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>Identifier of the worker that claimed this deployment.</summary>
    public string? WorkerId { get; private set; }

    public Guid? BuildId { get; private set; }
    public Build? Build { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyCollection<DeploymentLog> Logs => _logs;

    public static Deployment Queue(Guid applicationId, int number, bool simulateFailure, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);

        return new Deployment
        {
            Id = Guid.CreateVersion7(now),
            ApplicationId = applicationId,
            Number = number,
            Version = $"v{number}",
            Status = DeploymentStatus.Queued,
            SimulateFailure = simulateFailure,
            CreatedAt = now,
        };
    }

    /// <summary>Queued → Running, performed when a worker claims the deployment.</summary>
    public void Start(string workerId, DateTimeOffset now)
    {
        EnsureStatus(DeploymentStatus.Queued, nameof(Start));
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        Status = DeploymentStatus.Running;
        WorkerId = workerId;
        StartedAt = now;
    }

    public void EnterStage(DeploymentStage stage)
    {
        EnsureStatus(DeploymentStatus.Running, nameof(EnterStage));

        var expected = DeploymentPipeline.NextAfter(CurrentStage);
        if (stage != expected)
        {
            throw new InvalidStateTransitionException(
                $"Deployment {Id} cannot enter stage {stage}; the next stage is {expected?.ToString() ?? "none"}.");
        }

        CurrentStage = stage;
    }

    /// <summary>Records which commit this deployment is building, once the source has been fetched.</summary>
    /// <param name="commitSha">A full 40-character Git commit id.</param>
    public void RecordCommit(string commitSha)
    {
        EnsureStatus(DeploymentStatus.Running, nameof(RecordCommit));

        var normalised = commitSha.Trim().ToLowerInvariant();
        if (normalised.Length != CommitShaMaxLength || !normalised.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException($"'{commitSha}' is not a full Git commit id.", nameof(commitSha));
        }

        CommitSha = normalised;
    }

    /// <summary>Records where this deployment's application was made reachable.</summary>
    public void RecordUrl(string url)
    {
        EnsureStatus(DeploymentStatus.Running, nameof(RecordUrl));

        if (url.Length > UrlMaxLength
            || !Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"'{url}' is not an http(s) URL.", nameof(url));
        }

        Url = url;
    }

    public void AttachBuild(Build build)
    {
        EnsureStatus(DeploymentStatus.Running, nameof(AttachBuild));
        Build = build;
        BuildId = build.Id;
    }

    public void Succeed(DateTimeOffset now)
    {
        EnsureStatus(DeploymentStatus.Running, nameof(Succeed));
        if (DeploymentPipeline.NextAfter(CurrentStage) is { } remaining)
        {
            throw new InvalidStateTransitionException(
                $"Deployment {Id} cannot succeed before stage {remaining} has run.");
        }

        Status = DeploymentStatus.Succeeded;
        CompletedAt = now;
    }

    public void Fail(string errorMessage, DateTimeOffset now)
    {
        EnsureStatus(DeploymentStatus.Running, nameof(Fail));

        Status = DeploymentStatus.Failed;
        ErrorMessage = Truncate(errorMessage);
        CompletedAt = now;
    }

    private static string Truncate(string value) =>
        value.Length > ErrorMessageMaxLength ? value[..ErrorMessageMaxLength] : value;

    private void EnsureStatus(DeploymentStatus required, string operation)
    {
        if (Status != required)
        {
            throw new InvalidStateTransitionException(
                $"Deployment {Id} is {Status}; {operation} requires it to be {required}.");
        }
    }
}
