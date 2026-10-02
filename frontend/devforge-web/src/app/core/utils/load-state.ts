import { Signal, computed, signal } from '@angular/core';
import { Observable, Subscription } from 'rxjs';
import { ApiError } from '../models/api-error';

/**
 * Holds the result of a (possibly repeating) request as signals: the latest value, the error that
 * stopped it, and whether the first value is still pending.
 */
export class LoadState<T> {
  private readonly _value = signal<T | null>(null);
  private readonly _error = signal<ApiError | null>(null);
  private subscription?: Subscription;

  readonly value: Signal<T | null> = this._value.asReadonly();
  readonly error: Signal<ApiError | null> = this._error.asReadonly();
  readonly loading = computed(() => this._value() === null && this._error() === null);

  /** Starts (or restarts) the request. Any previous subscription is cancelled. */
  load(source: Observable<T>): void {
    this.subscription?.unsubscribe();
    this._error.set(null);
    this.subscription = source.subscribe({
      next: (value) => this._value.set(value),
      error: (error: unknown) => this._error.set(ApiError.from(error)),
    });
  }

  stop(): void {
    this.subscription?.unsubscribe();
  }
}
