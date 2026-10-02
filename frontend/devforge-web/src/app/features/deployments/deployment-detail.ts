import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { interval, map } from 'rxjs';
import { ErrorAlert } from '../../shared/components/error-alert';
import { Loading } from '../../shared/components/loading';
import { StatusBadge } from '../../shared/components/status-badge';
import { DurationPipe } from '../../shared/pipes/duration.pipe';
import { DeploymentLogs } from './deployment-logs';
import { DeploymentTracker } from './deployment-tracker';
import { PipelineSteps } from './pipeline-steps';

const CLOCK_TICK_MS = 1000;

@Component({
  selector: 'app-deployment-detail',
  imports: [RouterLink, DatePipe, DurationPipe, StatusBadge, Loading, ErrorAlert, PipelineSteps, DeploymentLogs],
  providers: [DeploymentTracker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './deployment-detail.html',
  styleUrl: './deployment-detail.scss',
})
export class DeploymentDetail {
  /** Bound from the `:id` route parameter. */
  readonly id = input.required<string>();

  protected readonly tracker = inject(DeploymentTracker);

  /** Ticks once a second so the duration of a running deployment counts up between polls. */
  private readonly now = toSignal(interval(CLOCK_TICK_MS).pipe(map(() => Date.now())), { initialValue: Date.now() });

  /** Milliseconds from start to completion, or to now while still running. `null` before a worker starts it. */
  protected readonly elapsed = computed(() => {
    const deployment = this.tracker.deployment();
    if (!deployment?.startedAt) {
      return null;
    }

    const end = deployment.completedAt ? new Date(deployment.completedAt).getTime() : this.now();
    return end - new Date(deployment.startedAt).getTime();
  });

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => this.tracker.track(id));
    });
  }

  protected resume(): void {
    this.tracker.resume(this.id());
  }
}
