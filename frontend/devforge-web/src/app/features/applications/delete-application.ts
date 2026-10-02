import { Injectable, inject, signal } from '@angular/core';
import { Application } from '../../core/models/application';
import { ApiError } from '../../core/models/api-error';
import { ApplicationsApi } from '../../core/services/applications-api';
import { Notifications } from '../../core/services/notifications';

/**
 * The confirm-then-delete flow shared by the list and detail pages. Provide it on the component
 * so each page gets its own instance.
 */
@Injectable()
export class DeleteApplication {
  private readonly api = inject(ApplicationsApi);
  private readonly notifications = inject(Notifications);

  /** The application awaiting confirmation, or `null` when the dialog is closed. */
  readonly target = signal<Application | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  request(application: Application): void {
    this.error.set(null);
    this.target.set(application);
  }

  cancel(): void {
    this.target.set(null);
  }

  confirm(onDeleted: () => void): void {
    const application = this.target();
    if (!application) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    this.api.delete(application.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.target.set(null);
        this.notifications.success(`Deleted ${application.name}`);
        onDeleted();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.error.set(ApiError.from(error).message);
      },
    });
  }
}
