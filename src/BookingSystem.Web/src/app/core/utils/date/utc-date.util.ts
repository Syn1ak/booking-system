/**
 * Dates are UTC calendar dates as `yyyy-MM-dd`, matching the API's `DateOnly`. Everything goes
 * through `Date.UTC` and the `getUTC*` accessors: the local-time constructors would put
 * "today" on yesterday for anyone west of Greenwich late in the evening.
 */

export type TDateParts = { year: number; month: number; day: number };

export function todayUtc(now: Date = new Date()): string {
  return now.toISOString().slice(0, 10);
}

export function addDays(date: string, days: number): string {
  const { year, month, day } = toDateParts(date);
  return new Date(Date.UTC(year, month - 1, day + days)).toISOString().slice(0, 10);
}

export function utcDateOf(instant: string): string {
  return new Date(instant).toISOString().slice(0, 10);
}

export function toDateParts(date: string): TDateParts {
  const [year, month, day] = date.split('-').map(Number);
  return { year, month, day };
}

export function fromDateParts({ year, month, day }: TDateParts): string {
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
}

export function isPast(instant: string, now: Date = new Date()): boolean {
  return new Date(instant).getTime() <= now.getTime();
}
