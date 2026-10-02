import { Injectable, signal } from '@angular/core';

export type NotificationKind = 'success' | 'error';

export interface Notification {
  id: number;
  kind: NotificationKind;
  message: string;
}

const AUTO_DISMISS_MS = 4000;

/** Short-lived messages confirming an action, shown by the toast outlet in the app shell. */
@Injectable({ providedIn: 'root' })
export class Notifications {
  private nextId = 1;
  private readonly _items = signal<Notification[]>([]);

  readonly items = this._items.asReadonly();

  success(message: string): void {
    this.show('success', message);
  }

  error(message: string): void {
    this.show('error', message);
  }

  dismiss(id: number): void {
    this._items.update((items) => items.filter((item) => item.id !== id));
  }

  private show(kind: NotificationKind, message: string): void {
    const id = this.nextId++;
    this._items.update((items) => [...items, { id, kind, message }]);
    setTimeout(() => this.dismiss(id), AUTO_DISMISS_MS);
  }
}
