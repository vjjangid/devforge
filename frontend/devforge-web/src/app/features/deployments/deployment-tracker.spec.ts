import { TestBed } from '@angular/core/testing';
import { Observable, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ApiError } from '../../core/models/api-error';
import { Deployment, DeploymentLog } from '../../core/models/deployment';
import { DeploymentsApi } from '../../core/services/deployments-api';
import { POLL_INTERVAL_MS } from '../../core/utils/poll-while';
import { aDeployment, aLog } from '../../testing/fixtures';
import { DeploymentTracker } from './deployment-tracker';

describe('DeploymentTracker', () => {
  let get: ReturnType<typeof vi.fn<(id: string) => Observable<Deployment>>>;
  let logs: ReturnType<typeof vi.fn<(id: string, afterId?: number) => Observable<DeploymentLog[]>>>;
  let tracker: DeploymentTracker;

  beforeEach(() => {
    vi.useFakeTimers();
    get = vi.fn();
    logs = vi.fn();

    TestBed.configureTestingModule({
      providers: [DeploymentTracker, { provide: DeploymentsApi, useValue: { get, logs } }],
    });
    tracker = TestBed.inject(DeploymentTracker);
  });

  afterEach(() => vi.useRealTimers());

  it('polls until the deployment finishes, appending only new log lines', async () => {
    get
      .mockReturnValueOnce(of(aDeployment('Queued')))
      .mockReturnValueOnce(of(aDeployment('Running')))
      .mockReturnValueOnce(of(aDeployment('Succeeded')));
    logs
      .mockReturnValueOnce(of([aLog(1, 'queued')]))
      .mockReturnValueOnce(of([aLog(2, 'started'), aLog(3, 'building')]))
      .mockReturnValueOnce(of([aLog(4, 'done')]));

    tracker.track('dep-1');
    await vi.advanceTimersByTimeAsync(0);

    expect(tracker.deployment()?.status).toBe('Queued');
    expect(tracker.active()).toBe(true);
    expect(logs).toHaveBeenLastCalledWith('dep-1', undefined);

    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    expect(tracker.deployment()?.status).toBe('Running');
    expect(logs).toHaveBeenLastCalledWith('dep-1', 1);

    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    expect(tracker.deployment()?.status).toBe('Succeeded');
    expect(tracker.active()).toBe(false);
    expect(logs).toHaveBeenLastCalledWith('dep-1', 3);
    expect(tracker.logs().map((entry) => entry.message)).toEqual(['queued', 'started', 'building', 'done']);

    // Terminal state reached: no further requests.
    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS * 5);
    expect(get).toHaveBeenCalledTimes(3);
  });

  it('fetches a finished deployment exactly once', async () => {
    get.mockReturnValue(of(aDeployment('Failed', { errorMessage: 'Tests failed' })));
    logs.mockReturnValue(of([aLog(1, 'queued')]));

    tracker.track('dep-1');
    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS * 3);

    expect(get).toHaveBeenCalledTimes(1);
    expect(tracker.deployment()?.errorMessage).toBe('Tests failed');
  });

  it('reports an error and stops polling when a request fails', async () => {
    get.mockReturnValue(throwError(() => new ApiError(404, 'Deployment was not found.')));

    tracker.track('missing');
    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS * 3);

    expect(tracker.error()?.isNotFound).toBe(true);
    expect(tracker.loading()).toBe(false);
    expect(get).toHaveBeenCalledTimes(1);
  });

  it('resumes after an error without losing what was already loaded', async () => {
    get.mockReturnValueOnce(of(aDeployment('Running'))).mockReturnValueOnce(throwError(() => new ApiError(0, 'offline')));
    logs.mockReturnValueOnce(of([aLog(1, 'queued')]));

    tracker.track('dep-1');
    await vi.advanceTimersByTimeAsync(POLL_INTERVAL_MS);
    expect(tracker.error()?.message).toBe('offline');

    get.mockReturnValueOnce(of(aDeployment('Succeeded')));
    logs.mockReturnValueOnce(of([aLog(2, 'done')]));
    tracker.resume('dep-1');
    await vi.advanceTimersByTimeAsync(0);

    expect(tracker.error()).toBeNull();
    expect(logs).toHaveBeenLastCalledWith('dep-1', 1);
    expect(tracker.logs().map((entry) => entry.message)).toEqual(['queued', 'done']);
  });
});
