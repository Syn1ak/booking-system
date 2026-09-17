import { TimeOnlyPipe } from './time-only.pipe';
import { UtcDatePipe } from './utc-date.pipe';
import { UtcTimePipe } from './utc-time.pipe';

describe('date pipes', () => {
  it('formats a bare date on the UTC calendar', () => {
    expect(new UtcDatePipe().transform('2026-09-17', 'medium')).toBe('Thu, 17 Sept 2026');
  });

  it('formats an instant on the UTC clock', () => {
    expect(new UtcTimePipe().transform('2026-09-17T09:30:00Z')).toBe('09:30');
  });

  it('trims seconds from a TimeOnly', () => {
    expect(new TimeOnlyPipe().transform('17:00:00')).toBe('17:00');
  });
});
