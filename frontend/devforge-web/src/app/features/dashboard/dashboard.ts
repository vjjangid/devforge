import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardSummary } from '../../core/models/dashboard';
import { isDeploymentActive } from '../../core/models/deployment';
import { DashboardApi } from '../../core/services/dashboard-api';
import { LoadState } from '../../core/utils/load-state';
import { pollWhile } from '../../core/utils/poll-while';
import { EmptyState } from '../../shared/components/empty-state';
import { ErrorAlert } from '../../shared/components/error-alert';
import { Loading } from '../../shared/components/loading';
import { StatusBadge } from '../../shared/components/status-badge';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, DatePipe, StatusBadge, Loading, ErrorAlert, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly api = inject(DashboardApi);

  protected readonly state = new LoadState<DashboardSummary>();

  constructor() {
    inject(DestroyRef).onDestroy(() => this.state.stop());
    this.load();
  }

  /** Keeps the numbers live while a deployment is in flight, then stops polling. */
  protected load(): void {
    this.state.load(
      pollWhile(
        () => this.api.summary(),
        (summary) => summary.recentDeployments.some((deployment) => isDeploymentActive(deployment.status)),
      ),
    );
  }
}
