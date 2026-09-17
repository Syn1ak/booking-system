import { Injectable } from '@angular/core';
import { NgbDateAdapter, NgbDateStruct } from '@ng-bootstrap/ng-bootstrap';
import { fromDateParts, toDateParts } from '../utils/date/utc-date.util';

/** Datepicker models hold the API's `yyyy-MM-dd` directly, so forms never see a date struct. */
@Injectable()
export class IsoDateAdapter extends NgbDateAdapter<string> {
  fromModel(value: string | null): NgbDateStruct | null {
    return value && /^\d{4}-\d{2}-\d{2}$/.test(value) ? toDateParts(value) : null;
  }

  toModel(date: NgbDateStruct | null): string | null {
    return date ? fromDateParts(date) : null;
  }
}
