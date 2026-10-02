import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Placeholder for a list with nothing in it. Project a call-to-action as content. */
@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="empty">
      <h2>{{ title() }}</h2>
      <p class="muted">{{ message() }}</p>
      <ng-content />
    </div>
  `,
  styles: `
    .empty {
      display: grid;
      justify-items: center;
      gap: var(--space-2);
      padding: var(--space-6) var(--space-4);
      text-align: center;
    }

    p {
      max-width: 420px;
      margin-bottom: var(--space-2);
    }
  `,
})
export class EmptyState {
  readonly title = input.required<string>();
  readonly message = input.required<string>();
}
