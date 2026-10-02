import { ChangeDetectionStrategy, Component, ElementRef, effect, input, output, viewChild } from '@angular/core';

/** A modal confirmation for destructive actions, built on the native dialog element. */
@Component({
  selector: 'app-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog #dialog (cancel)="onNativeCancel($event)">
      <h2>{{ title() }}</h2>
      <p class="muted">{{ message() }}</p>
      @if (error(); as error) {
        <p class="error" role="alert">{{ error }}</p>
      }
      <div class="actions">
        <button type="button" class="btn" [disabled]="busy()" (click)="cancelled.emit()">Cancel</button>
        <button type="button" class="btn btn-danger" [disabled]="busy()" (click)="confirmed.emit()">
          {{ busy() ? busyLabel() : confirmLabel() }}
        </button>
      </div>
    </dialog>
  `,
  styles: `
    dialog {
      width: min(440px, calc(100vw - 32px));
      padding: var(--space-5);
      border: 1px solid var(--border);
      border-radius: var(--radius);
      background: var(--surface);
      color: var(--text);
    }

    dialog::backdrop {
      background: rgb(8 10 16 / 55%);
    }

    p {
      margin-top: var(--space-2);
    }

    .error {
      color: var(--danger);
    }

    .actions {
      display: flex;
      justify-content: flex-end;
      gap: var(--space-2);
      margin-top: var(--space-5);
    }
  `,
})
export class ConfirmDialog {
  readonly open = input.required<boolean>();
  readonly title = input.required<string>();
  readonly message = input.required<string>();
  readonly confirmLabel = input('Delete');
  readonly busyLabel = input('Deleting…');
  readonly busy = input(false);
  readonly error = input<string | null>(null);

  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    effect(() => {
      const element = this.dialog().nativeElement;
      if (this.open() && !element.open) {
        element.showModal();
      } else if (!this.open() && element.open) {
        element.close();
      }
    });
  }

  /** Escape closes native dialogs on its own; route it through the owner's state instead. */
  protected onNativeCancel(event: Event): void {
    event.preventDefault();
    if (!this.busy()) {
      this.cancelled.emit();
    }
  }
}
