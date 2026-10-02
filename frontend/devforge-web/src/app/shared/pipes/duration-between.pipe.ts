import { Pipe, PipeTransform } from '@angular/core';
import { formatDuration } from './duration.pipe';

/** Formats the time between two ISO timestamps; `—` while either end is missing. */
@Pipe({ name: 'durationBetween' })
export class DurationBetweenPipe implements PipeTransform {
  transform(start: string | null, end: string | null): string {
    if (!start || !end) {
      return '—';
    }

    return formatDuration(new Date(end).getTime() - new Date(start).getTime());
  }
}
