import { Pipe, PipeTransform } from '@angular/core';

export type TUtcDateFormat = 'long' | 'medium' | 'short' | 'weekday' | 'day' | 'month';

const FORMATS: Record<TUtcDateFormat, Intl.DateTimeFormatOptions> = {
  long: { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' },
  medium: { weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' },
  short: { day: 'numeric', month: 'short' },
  weekday: { weekday: 'short' },
  day: { day: 'numeric' },
  month: { month: 'short' },
};

/**
 * Formats a `yyyy-MM-dd` date or an ISO instant on the UTC calendar. Angular's DatePipe reads a
 * bare `yyyy-MM-dd` as local midnight, which shifts the day for anyone east of Greenwich.
 */
@Pipe({ name: 'utcDate' })
export class UtcDatePipe implements PipeTransform {
  transform(value: string | null | undefined, format: TUtcDateFormat = 'medium'): string {
    if (!value) {
      return '';
    }

    const instant = value.length === 10 ? `${value}T00:00:00Z` : value;
    return new Intl.DateTimeFormat('en-GB', { ...FORMATS[format], timeZone: 'UTC' }).format(
      new Date(instant),
    );
  }
}
