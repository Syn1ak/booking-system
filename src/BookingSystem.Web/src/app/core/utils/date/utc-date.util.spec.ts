import { addDays, fromDateParts, isPast, toDateParts, todayUtc, utcDateOf } from './utc-date.util';

describe('utc-date util', () => {
  it('takes today from the UTC calendar, not the local one', () => {
    expect(todayUtc(new Date('2026-09-17T23:30:00-05:00'))).toBe('2026-09-18');
  });

  it('adds days across month and year boundaries', () => {
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2026-03-01', -1)).toBe('2026-02-28');
  });

  it('reads the UTC date of an instant', () => {
    expect(utcDateOf('2026-09-17T23:00:00Z')).toBe('2026-09-17');
  });

  it('round-trips date parts', () => {
    expect(toDateParts('2026-09-07')).toEqual({ year: 2026, month: 9, day: 7 });
    expect(fromDateParts({ year: 2026, month: 9, day: 7 })).toBe('2026-09-07');
  });

  it('treats an instant that has arrived as past', () => {
    const now = new Date('2026-09-17T10:00:00Z');
    expect(isPast('2026-09-17T10:00:00Z', now)).toBe(true);
    expect(isPast('2026-09-17T10:00:01Z', now)).toBe(false);
  });
});
