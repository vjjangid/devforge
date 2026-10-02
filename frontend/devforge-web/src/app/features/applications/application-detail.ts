import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { catchError, forkJoin, map, of } from 'rxjs';
import { ApiError } from '../../core/models/api-error';
import { Application } from '../../core/models/application';
import { Deployment } from '../../core/models/deployment';
import { ApplicationsApi } from '../../core/services/applications-api';
import { DeploymentsApi } from '../../core/services/deployments-api';
import { Notifications } from '../../core/services/notifications';
import { PlatformApi } from '../../core/services/platform-api';
import { LoadState } from '../../core/utils/load-state';
import { pollWhile } from '../../core/utils/poll-while';
import { ConfirmDialog } from '../../shared/components/confirm-dialog';
import { EmptyState } from '../../shared/components/empty-state';
import { ErrorAlert } from '../../shared/components/error-alert';
import { Loading } from '../../shared/components/loading';
import { StatusBadge } from '../../shared/components/status-badge';
import { DurationBetweenPipe } from '../../shared/pipes/duration-between.pipe';
import { DeleteApplication } from './delete-application';

interface ApplicationView {
  application: Application;
  deployments: Deployment[];
}

@Component({
  selector: 'app-application-detail',
  imports: [RouterLink, DatePipe, DurationBetweenPipe, StatusBadge, Loading, ErrorAlert, EmptyState, ConfirmDialog],
  providers: [DeleteApplication],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './application-detail.html',
  styleUrl: './application-detail.scss',
})
export class ApplicationDetail {
  private readonly applications = inject(ApplicationsApi);
  private readonly deployments = inject(DeploymentsApi);
  private readonly notifications = inject(Notifications);
  private readonly router = inject(Router);

  /** Bound from the `:id` route parameter. */
  readonly id = input.required<string>();

  protected readonly state = new LoadState<ApplicationView>();
  protected readonly deletion = inject(DeleteApplication);

  protected readonly simulateFailure = signal(false);
  protected readonly deploying = signal(false);
  protected readonly deployError = signal<string | null>(null);

  /** The failure switch is a development aid; hide it when the platform has it turned off (or unknown). */
  protected readonly failureSimulationAvailable = toSignal(
    inject(PlatformApi)
      .info()
      .pipe(
        map((info) => info.features.failureSimulation),
        catchError(() => of(false)),
      ),
    { initialValue: false },
  );

  constructor() {
    inject(DestroyRef).onDestroy(() => this.state.stop());
    effect(() => {
      const id = this.id();
      untracked(() => this.load(id));
    });
  }

  protected reload(): void {
    this.load(this.id());
  }

  protected deploy(): void {
    this.deploying.set(true);
    this.deployError.set(null);

    this.deployments.create(this.id(), { simulateFailure: this.simulateFailure() }).subscribe({
      next: (deployment) => {
        this.notifications.success(`Deployment ${deployment.version} queued`);
        void this.router.navigate(['/deployments', deployment.id]);
      },
      error: (error: unknown) => {
        this.deploying.set(false);
        this.deployError.set(ApiError.from(error).message);
      },
    });
  }

  protected confirmDelete(): void {
    this.deletion.confirm(() => void this.router.navigate(['/applications']));
  }

  protected toggleSimulateFailure(event: Event): void {
    this.simulateFailure.set((event.target as HTMLInputElement).checked);
  }

  private load(id: string): void {
    this.state.load(
      pollWhile(
        () =>
          forkJoin({
            application: this.applications.get(id),
            deployments: this.deployments.listForApplication(id),
          }),
        (view) => view.application.status === 'Deploying',
      ),
    );
  }
}
