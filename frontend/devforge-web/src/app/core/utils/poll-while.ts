import { Observable, exhaustMap, takeWhile, timer } from 'rxjs';

export const POLL_INTERVAL_MS = 2000;

/**
 * Emits the result of `fetch` immediately and then again every `intervalMs`, for as long as
 * `shouldContinue` holds for the latest value. The value that ends the polling is still emitted.
 * A slow request is never overlapped by the next tick.
 */
export function pollWhile<T>(
  fetch: () => Observable<T>,
  shouldContinue: (value: T) => boolean,
  intervalMs: number = POLL_INTERVAL_MS,
): Observable<T> {
  return timer(0, intervalMs).pipe(
    exhaustMap(() => fetch()),
    takeWhile(shouldContinue, true),
  );
}
