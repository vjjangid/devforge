namespace DevForge.Infrastructure.Execution;

/// <summary>Answers "is something serving HTTP at this address yet?". An interface so tests need no real server.</summary>
internal interface IHealthProbe
{
    Task<bool> IsRespondingAsync(Uri url, CancellationToken cancellationToken);
}
