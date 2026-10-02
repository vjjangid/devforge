import { Pipe, PipeTransform } from '@angular/core';

const MS_PER_SECOND = 1000;
const SECONDS_PER_MINUTE = 60;
const MINUTES_PER_HOUR = 60;

/** Formats a number of milliseconds as `45s`, `1m 12s` or `2h 03m`. */
export function formatDuration(milliseconds: number): string {
  const totalSeconds = Math.max(0, Math.floor(milliseconds / MS_PER_SECOND));
  const seconds = totalSeconds % SECONDS_PER_MINUTE;
  const totalMinutes = Math.floor(totalSeconds / SECONDS_PER_MINUTE);

  if (totalMinutes === 0) {
    return `${seconds}s`;
  }

  if (totalMinutes < MINUTES_PER_HOUR) {
    return `${totalMinutes}m ${seconds}s`;
  }

  const hours = Math.floor(totalMinutes / MINUTES_PER_HOUR);
  const minutes = totalMinutes % MINUTES_PER_HOUR;
  return `${hours}h ${String(minutes).padStart(2, '0')}m`;
}

@Pipe({ name: 'duration' })
export class DurationPipe implements PipeTransform {
  transform(milliseconds: number | null | undefined): string {
    return milliseconds === null || milliseconds === undefined ? '—' : formatDuration(milliseconds);
  }
}
