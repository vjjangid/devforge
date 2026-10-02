using System.Data.Common;
using DevForge.Application.Pipeline;
using Microsoft.Extensions.Options;

namespace DevForge.Worker;

/// <summary>
/// Polls for queued deployments and processes them one at a time. Run more instances of this
/// service to process deployments in parallel; the queue guarantees each is claimed only once.
/// </summary>
internal sealed class DeploymentWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WorkerOptions> options,
    TimeProvider clock,
    ILogger<DeploymentWorker> logger) : BackgroundService
{
    private readonly string _workerId = string.IsNullOrWhiteSpace(options.Value.WorkerId)
        ? $"{Environment.MachineName}-{Guid.NewGuid().ToString("N")[..8]}"
        : options.Value.WorkerId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingInterval = options.Value.PollingInterval;
        logger.LogInformation(
            "Worker {WorkerId} started, polling every {PollingInterval}",
            _workerId,
            pollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessNextAsync(stoppingToken);

                // After a deployment, look again straight away: more may be waiting.
                if (!processed)
                {
                    await Task.Delay(pollingInterval, clock, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (DbException exception)
            {
                // The database is unreachable or not migrated yet. Expected during startup, so no stack trace.
                logger.LogWarning(
                    "Worker {WorkerId} could not reach the deployment queue ({Reason}); retrying in {PollingInterval}",
                    _workerId,
                    exception.Message,
                    pollingInterval);
                await DelayQuietlyAsync(pollingInterval, stoppingToken);
            }
            catch (Exception exception)
            {
                // Keep the worker alive: one bad iteration must not stop deployments from being processed.
                logger.LogError(exception, "Worker {WorkerId} failed to process the queue; retrying", _workerId);
                await DelayQuietlyAsync(pollingInterval, stoppingToken);
            }
        }

        logger.LogInformation("Worker {WorkerId} stopped", _workerId);
    }

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        // A fresh scope (and DbContext) per iteration keeps the change tracker small and isolated.
        await using var scope = scopeFactory.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<DeploymentProcessor>();
        return await processor.ProcessNextAsync(_workerId, cancellationToken);
    }

    private async Task DelayQuietlyAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, clock, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down; the loop condition ends the worker.
        }
    }
}
