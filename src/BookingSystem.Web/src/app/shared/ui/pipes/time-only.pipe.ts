import { Pipe, PipeTransform } from '@angular/core';

/** The API's `TimeOnly` (`HH:mm:ss`) as `HH:mm`. */
@Pipe({ name: 'timeOnly' })
export class TimeOnlyPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return value ? value.slice(0, 5) : '';
  }
}
