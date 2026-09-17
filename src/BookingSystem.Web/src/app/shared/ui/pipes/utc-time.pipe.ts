import { Pipe, PipeTransform } from '@angular/core';

const TIME_FORMAT = new Intl.DateTimeFormat('en-GB', {
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
  timeZone: 'UTC',
});

/** An ISO instant as `HH:mm` on the UTC clock. */
@Pipe({ name: 'utcTime' })
export class UtcTimePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return value ? TIME_FORMAT.format(new Date(value)) : '';
  }
}
