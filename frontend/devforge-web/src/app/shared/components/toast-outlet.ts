import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Notifications } from '../../core/services/notifications';

@Component({
  selector: 'app-toast-outlet',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="toasts" aria-live="polite">
      @for (item of notifications.items(); track item.id) {
        <div class="toast" [class]="item.kind">
          <span>{{ item.message }}</span>
          <button type="button" aria-label="Dismiss" (click)="notifications.dismiss(item.id)">×</button>
        </div>
      }
    </div>
  `,
  styles: `
    .toasts {
      position: fixed;
      right: var(--space-4);
      bottom: var(--space-4);
      z-index: 10;
      display: grid;
      gap: var(--space-2);
      width: min(360px, calc(100vw - 32px));
    }

    .toast {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--space-3);
      padding: var(--space-3) var(--space-4);
      border: 1px solid var(--border);
      border-left-width: 4px;
      border-radius: var(--radius);
      background: var(--surface);
      box-shadow: 0 6px 20px rgb(8 10 16 / 18%);
    }

    .success { border-left-color: var(--success); }
    .error { border-left-color: var(--danger); }

    button {
      border: 0;
      background: none;
      color: var(--text-muted);
      font-size: 18px;
      line-height: 1;
      cursor: pointer;
    }
  `,
})
export class ToastOutlet {
  protected readonly notifications = inject(Notifications);
}
