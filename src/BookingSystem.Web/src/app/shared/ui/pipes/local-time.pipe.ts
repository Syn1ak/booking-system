import { Pipe, PipeTransform } from '@angular/core';

const LOCAL_FORMAT = new Intl.DateTimeFormat(undefined, {
  weekday: 'short',
  hour: '2-digit',
  minute: '2-digit',
  timeZoneName: 'short',
});

/** An ISO instant in the viewer's own time zone, for hints beside the UTC display. */
@Pipe({ name: 'localTime' })
export class LocalTimePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return value ? LOCAL_FORMAT.format(new Date(value)) : '';
  }
}
