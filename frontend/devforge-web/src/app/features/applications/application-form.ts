import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { ApiError } from '../../core/models/api-error';
import { Application } from '../../core/models/application';
import { Runtime } from '../../core/models/platform';
import { ApplicationsApi } from '../../core/services/applications-api';
import { Notifications } from '../../core/services/notifications';
import { PlatformApi } from '../../core/services/platform-api';
import { ErrorAlert } from '../../shared/components/error-alert';
import { Loading } from '../../shared/components/loading';

// Mirrors the limits the API enforces, so most mistakes are caught before a round trip.
const NAME_MAX_LENGTH = 100;
const REPOSITORY_URL_MAX_LENGTH = 500;
const BRANCH_MAX_LENGTH = 255;
const DESCRIPTION_MAX_LENGTH = 1000;

/** Optional http(s) scheme, a host, and at least one path segment. */
const REPOSITORY_URL_PATTERN = /^(https?:\/\/)?[^\s/@]+\/\S+$/i;
const NO_WHITESPACE_PATTERN = /^\S+$/;

/** Validation error key used for messages that came back from the API. */
const SERVER_ERROR = 'server';

type FieldName = 'name' | 'repositoryUrl' | 'branch' | 'runtime' | 'description';

const FIELD_LABELS: Record<FieldName, string> = {
  name: 'Name',
  repositoryUrl: 'Repository URL',
  branch: 'Branch',
  runtime: 'Runtime',
  description: 'Description',
};

/** Creates an application, or edits one when the route carries an `id`. */
@Component({
  selector: 'app-application-form',
  imports: [ReactiveFormsModule, RouterLink, Loading, ErrorAlert],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './application-form.html',
  styleUrl: './application-form.scss',
})
export class ApplicationForm {
  private readonly api = inject(ApplicationsApi);
  private readonly notifications = inject(Notifications);
  private readonly router = inject(Router);

  /** Bound from the `:id` route parameter; absent on the "new" route. */
  readonly id = input<string>();

  protected readonly isEdit = computed(() => this.id() !== undefined);
  protected readonly cancelLink = computed(() => (this.isEdit() ? ['/applications', this.id()] : ['/applications']));

  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(NAME_MAX_LENGTH)]],
    repositoryUrl: [
      '',
      [Validators.required, Validators.maxLength(REPOSITORY_URL_MAX_LENGTH), Validators.pattern(REPOSITORY_URL_PATTERN)],
    ],
    branch: [
      'main',
      [Validators.required, Validators.maxLength(BRANCH_MAX_LENGTH), Validators.pattern(NO_WHITESPACE_PATTERN)],
    ],
    runtime: ['', Validators.required],
    description: ['', Validators.maxLength(DESCRIPTION_MAX_LENGTH)],
  });

  protected readonly runtimes = toSignal(
    inject(PlatformApi)
      .info()
      .pipe(
        map((info): Runtime[] => info.runtimes),
        catchError(() => of<Runtime[]>([])),
      ),
  );

  /** In edit mode: `true` until the application has been fetched into the form. */
  protected readonly loadingApplication = signal(false);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly saving = signal(false);
  protected readonly submitError = signal<string | null>(null);
  protected readonly submitted = signal(false);

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => (id === undefined ? this.prepareForCreate() : this.loadForEdit(id)));
    });

    // Preselect the runtime once the catalogue arrives, as long as the user has not chosen one.
    effect(() => {
      const first = this.runtimes()?.[0];
      if (first && !this.form.controls.runtime.value) {
        this.form.controls.runtime.setValue(first.key);
      }
    });
  }

  protected retryLoad(): void {
    const id = this.id();
    if (id !== undefined) {
      this.loadForEdit(id);
    }
  }

  protected submit(): void {
    this.submitted.set(true);
    this.submitError.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { name, repositoryUrl, branch, runtime, description } = this.form.getRawValue();
    const details = {
      name: name.trim(),
      repositoryUrl: repositoryUrl.trim(),
      branch: branch.trim(),
      description: description.trim() || null,
    };

    const id = this.id();
    const request = id === undefined ? this.api.create({ ...details, runtime }) : this.api.update(id, details);

    this.saving.set(true);
    request.subscribe({
      next: (application) => {
        this.notifications.success(id === undefined ? `Created ${application.name}` : `Saved ${application.name}`);
        void this.router.navigate(['/applications', application.id]);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.showSubmitError(ApiError.from(error));
      },
    });
  }

  /** The message to show under a field, or `null` while it is valid or still untouched. */
  protected errorFor(field: FieldName): string | null {
    const control = this.form.controls[field];
    if (control.valid || !(control.touched || this.submitted())) {
      return null;
    }

    const errors = control.errors ?? {};
    const label = FIELD_LABELS[field];

    if (errors[SERVER_ERROR]) {
      return errors[SERVER_ERROR] as string;
    }
    if (errors['required']) {
      return `${label} is required.`;
    }
    if (errors['maxlength']) {
      return `${label} must be at most ${errors['maxlength'].requiredLength} characters.`;
    }
    if (errors['pattern']) {
      return field === 'repositoryUrl'
        ? 'Enter a repository URL such as github.com/owner/repo.'
        : `${label} must not contain spaces.`;
    }

    return `${label} is invalid.`;
  }

  private prepareForCreate(): void {
    this.form.controls.runtime.enable();
  }

  private loadForEdit(id: string): void {
    this.loadingApplication.set(true);
    this.loadError.set(null);

    this.api.get(id).subscribe({
      next: (application) => {
        this.populate(application);
        this.loadingApplication.set(false);
      },
      error: (error: unknown) => {
        this.loadingApplication.set(false);
        this.loadError.set(ApiError.from(error));
      },
    });
  }

  private populate(application: Application): void {
    this.form.setValue({
      name: application.name,
      repositoryUrl: application.repositoryUrl,
      branch: application.branch,
      runtime: application.runtime,
      description: application.description ?? '',
    });

    // The runtime is fixed at creation; the API has no way to change it.
    this.form.controls.runtime.disable();
  }

  /** Field-level problems go under their inputs; everything else goes in the banner above the form. */
  private showSubmitError(error: ApiError): void {
    let attachedToField = false;

    for (const [field, messages] of Object.entries(error.fieldErrors)) {
      if (field in this.form.controls) {
        this.form.controls[field as FieldName].setErrors({ [SERVER_ERROR]: messages[0] });
        attachedToField = true;
      }
    }

    if (!attachedToField) {
      this.submitError.set(error.message);
    }
  }
}
