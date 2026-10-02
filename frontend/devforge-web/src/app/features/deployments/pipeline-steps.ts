import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PipelineStep, PipelineStepState } from '../../core/models/deployment';

const STATE_LABELS: Record<PipelineStepState, string> = {
  Pending: 'pending',
  Active: 'in progress',
  Completed: 'completed',
  Failed: 'failed',
};

/** The deployment pipeline as a row of steps, each showing whether it is done, running, failed or still to come. */
@Component({
  selector: 'app-pipeline-steps',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ol class="pipeline">
      @for (step of steps(); track step.name) {
        <li [class]="step.state.toLowerCase()" [attr.aria-current]="step.state === 'Active' ? 'step' : null">
          <span class="marker" aria-hidden="true">
            @switch (step.state) {
              @case ('Completed') { ✓ }
              @case ('Failed') { ✕ }
            }
          </span>
          <span class="name">{{ step.name }}</span>
          <span class="visually-hidden">{{ stateLabel(step.state) }}</span>
        </li>
      }
    </ol>
  `,
  styleUrl: './pipeline-steps.scss',
})
export class PipelineSteps {
  readonly steps = input.required<PipelineStep[]>();

  protected stateLabel(state: PipelineStepState): string {
    return STATE_LABELS[state];
  }
}
