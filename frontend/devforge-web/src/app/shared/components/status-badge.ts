import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ApplicationStatus } from '../../core/models/application';
import { DeploymentStatus } from '../../core/models/deployment';

type Tone = 'success' | 'danger' | 'info' | 'neutral';

interface BadgeView {
  label: string;
  tone: Tone;
  /** In-progress states get a pulsing dot. */
  busy?: boolean;
}

// "Running" means different things for the two kinds: a live application versus a deployment in progress.
const APPLICATION_VIEWS: Record<ApplicationStatus, BadgeView> = {
  NeverDeployed: { label: 'Never deployed', tone: 'neutral' },
  Deploying: { label: 'Deploying', tone: 'info', busy: true },
  Running: { label: 'Running', tone: 'success' },
  Failed: { label: 'Failed', tone: 'danger' },
};

const DEPLOYMENT_VIEWS: Record<DeploymentStatus, BadgeView> = {
  Queued: { label: 'Queued', tone: 'neutral', busy: true },
  Running: { label: 'Running', tone: 'info', busy: true },
  Succeeded: { label: 'Succeeded', tone: 'success' },
  Failed: { label: 'Failed', tone: 'danger' },
  Cancelled: { label: 'Cancelled', tone: 'neutral' },
};

@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="badge" [class]="view().tone" [class.busy]="view().busy">
      <span class="dot" aria-hidden="true"></span>{{ view().label }}
    </span>
  `,
  styles: `
    .badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 2px 10px 2px 8px;
      border-radius: 999px;
      font-size: 12px;
      font-weight: 600;
      white-space: nowrap;
    }

    .dot {
      width: 7px;
      height: 7px;
      border-radius: 50%;
      background: currentColor;
    }

    .busy .dot {
      animation: pulse 1.2s ease-in-out infinite;
    }

    .success { background: var(--success-bg); color: var(--success); }
    .danger { background: var(--danger-bg); color: var(--danger); }
    .info { background: var(--info-bg); color: var(--info); }
    .neutral { background: var(--neutral-bg); color: var(--neutral); }

    @keyframes pulse {
      50% { opacity: 0.3; }
    }

    @media (prefers-reduced-motion: reduce) {
      .busy .dot { animation: none; }
    }
  `,
})
export class StatusBadge {
  readonly kind = input.required<'application' | 'deployment'>();
  readonly status = input.required<ApplicationStatus | DeploymentStatus>();

  protected readonly view = computed<BadgeView>(() =>
    this.kind() === 'application'
      ? APPLICATION_VIEWS[this.status() as ApplicationStatus]
      : DEPLOYMENT_VIEWS[this.status() as DeploymentStatus],
  );
}
