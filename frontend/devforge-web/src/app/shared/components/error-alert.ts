import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'app-error-alert',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="alert" role="alert">
      <span class="message">{{ message() }}</span>
      @if (retryable()) {
        <button type="button" class="btn" (click)="retry.emit()">Retry</button>
      }
    </div>
  `,
  styles: `
    .alert {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-4);
      padding: var(--space-3) var(--space-4);
      border: 1px solid var(--danger);
      border-radius: var(--radius);
      background: var(--danger-bg);
      color: var(--danger);
    }

    .message {
      overflow-wrap: anywhere;
    }
  `,
})
export class ErrorAlert {
  readonly message = input.required<string>();
  readonly retryable = input(false);
  readonly retry = output<void>();
}
