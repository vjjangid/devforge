import { DurationBetweenPipe } from './duration-between.pipe';
import { DurationPipe, formatDuration } from './duration.pipe';

describe('formatDuration', () => {
  it.each([
    [0, '0s'],
    [999, '0s'],
    [45_000, '45s'],
    [72_000, '1m 12s'],
    [3_600_000, '1h 00m'],
    [7_380_000, '2h 03m'],
    [-5_000, '0s'],
  ])('formats %d ms as %s', (milliseconds, expected) => {
    expect(formatDuration(milliseconds)).toBe(expected);
  });
});

describe('DurationPipe', () => {
  it('shows a dash when there is no duration yet', () => {
    expect(new DurationPipe().transform(null)).toBe('—');
  });
});

describe('DurationBetweenPipe', () => {
  const pipe = new DurationBetweenPipe();

  it('formats the time between two timestamps', () => {
    expect(pipe.transform('2026-10-02T14:00:00Z', '2026-10-02T14:01:12Z')).toBe('1m 12s');
  });

  it('shows a dash while the deployment has not finished', () => {
    expect(pipe.transform('2026-10-02T14:00:00Z', null)).toBe('—');
  });
});
