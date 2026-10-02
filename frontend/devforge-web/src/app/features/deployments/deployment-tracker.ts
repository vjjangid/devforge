import { Injectable, OnDestroy, computed, inject, signal } from '@angular/core';
import { Subscription, map, switchMap } from 'rxjs';
import { ApiError } from '../../core/models/api-error';
import { Deployment, DeploymentLog, isDeploymentActive } from '../../core/models/deployment';
import { DeploymentsApi } from '../../core/services/deployments-api';
import { pollWhile } from '../../core/utils/poll-while';

/**
 * Follows one deployment: polls its status and appends new log lines until it reaches a terminal
 * state. Provide it on the component that displays the deployment.
 */
@Injectable()
export class DeploymentTracker implements OnDestroy {
  private readonly api = inject(DeploymentsApi);
  private subscription?: Subscription;

  private readonly _deployment = signal<Deployment | null>(null);
  private readonly _logs = signal<DeploymentLog[]>([]);
  private readonly _error = signal<ApiError | null>(null);

  readonly deployment = this._deployment.asReadonly();
  readonly logs = this._logs.asReadonly();
  readonly error = this._error.asReadonly();
  readonly loading = computed(() => this._deployment() === null && this._error() === null);
  readonly active = computed(() => {
    const deployment = this._deployment();
    return deployment !== null && isDeploymentActive(deployment.status);
  });

  track(id: string): void {
    this.subscription?.unsubscribe();
    this._deployment.set(null);
    this._logs.set([]);
    this._error.set(null);

    this.subscription = pollWhile(
      () => this.fetch(id),
      ({ deployment }) => isDeploymentActive(deployment.status),
    ).subscribe({
      next: ({ deployment, newLogs }) => {
        this._deployment.set(deployment);
        if (newLogs.length > 0) {
          this._logs.update((logs) => [...logs, ...newLogs]);
        }
      },
      error: (error: unknown) => this._error.set(ApiError.from(error)),
    });
  }

  /** Resumes after an error without discarding what is already on screen. */
  resume(id: string): void {
    const logs = this._logs();
    const deployment = this._deployment();
    this.track(id);
    this._deployment.set(deployment);
    this._logs.set(logs);
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }

  /**
   * Status first, logs second: when the status read says "finished", the log read that follows
   * is guaranteed to include the final lines.
   */
  private fetch(id: string) {
    return this.api.get(id).pipe(
      switchMap((deployment) =>
        this.api.logs(id, this._logs().at(-1)?.id).pipe(map((newLogs) => ({ deployment, newLogs }))),
      ),
    );
  }
}
