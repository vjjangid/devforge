import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Application } from '../../core/models/application';
import { ApplicationsApi } from '../../core/services/applications-api';
import { LoadState } from '../../core/utils/load-state';
import { pollWhile } from '../../core/utils/poll-while';
import { ConfirmDialog } from '../../shared/components/confirm-dialog';
import { EmptyState } from '../../shared/components/empty-state';
import { ErrorAlert } from '../../shared/components/error-alert';
import { Loading } from '../../shared/components/loading';
import { StatusBadge } from '../../shared/components/status-badge';
import { DeleteApplication } from './delete-application';

@Component({
  selector: 'app-application-list',
  imports: [RouterLink, DatePipe, StatusBadge, Loading, ErrorAlert, EmptyState, ConfirmDialog],
  providers: [DeleteApplication],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './application-list.html',
})
export class ApplicationList {
  private readonly api = inject(ApplicationsApi);

  protected readonly state = new LoadState<Application[]>();
  protected readonly deletion = inject(DeleteApplication);

  constructor() {
    inject(DestroyRef).onDestroy(() => this.state.stop());
    this.load();
  }

  protected confirmDelete(): void {
    this.deletion.confirm(() => this.load());
  }

  /** Refreshes on its own while any application is mid-deployment, so statuses settle without a reload. */
  protected load(): void {
    this.state.load(
      pollWhile(
        () => this.api.list(),
        (applications) => applications.some((application) => application.status === 'Deploying'),
      ),
    );
  }
}
