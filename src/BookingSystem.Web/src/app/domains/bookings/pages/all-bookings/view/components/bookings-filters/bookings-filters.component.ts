import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { NgbDateAdapter, NgbInputDatepicker } from '@ng-bootstrap/ng-bootstrap';
import { IsoDateAdapter } from '../../../../../../../core/providers/iso-date.adapter';
import { IRoom } from '../../../../../../../core/entities/rooms/room.dto';
import { BookingsFiltersFormHandler, RANGE_REVERSED } from '../../../models/bookings-filters.form';

@Component({
  selector: 'app-bookings-filters',
  imports: [ReactiveFormsModule, NgbInputDatepicker],
  template: `
    <form
      class="row g-3 align-items-end"
      [formGroup]="$form()"
      (ngSubmit)="$event.preventDefault()"
    >
      <div class="col-md-4">
        <label class="form-label small fw-medium" for="filter-room">Room</label>
        <select id="filter-room" class="form-select" [formControlName]="controlNames.roomId">
          <option [ngValue]="null">All rooms</option>
          @for (room of $rooms(); track room.roomId) {
            <option [ngValue]="room.roomId">{{ room.name }}</option>
          }
        </select>
      </div>
      @for (field of dateFields; track field.name) {
        <div class="col-sm-6 col-md-3">
          <label class="form-label small fw-medium" [for]="'filter-' + field.name">{{
            field.label
          }}</label>
          <div class="input-group">
            <input
              class="form-control"
              placeholder="yyyy-mm-dd"
              ngbDatepicker
              #picker="ngbDatepicker"
              [id]="'filter-' + field.name"
              [formControlName]="field.name"
              [class.is-invalid]="$form().hasError(rangeReversed)"
            />
            <button
              type="button"
              class="btn btn-outline-secondary"
              [attr.aria-label]="'Choose ' + field.label.toLowerCase() + ' date'"
              (click)="picker.toggle()"
            >
              <i class="bi bi-calendar3" aria-hidden="true"></i>
            </button>
          </div>
        </div>
      }
      <div class="col-md-2">
        <div class="form-check form-switch mb-2">
          <input
            id="filter-cancelled"
            class="form-check-input"
            type="checkbox"
            role="switch"
            [formControlName]="controlNames.includeCancelled"
          />
          <label class="form-check-label small" for="filter-cancelled">Show cancelled</label>
        </div>
      </div>
      @if ($form().hasError(rangeReversed)) {
        <div class="col-12 pt-0">
          <div class="invalid-feedback d-block mt-0">
            The “to” date must not be before the “from” date.
          </div>
        </div>
      }
    </form>
  `,
  providers: [{ provide: NgbDateAdapter, useClass: IsoDateAdapter }],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingsFiltersComponent {
  readonly rangeReversed = RANGE_REVERSED;
  readonly dateFields = [
    { name: 'from', label: 'From' },
    { name: 'to', label: 'To' },
  ] as const;

  $form = input.required<BookingsFiltersFormHandler>({ alias: 'form' });
  $rooms = input.required<IRoom[]>({ alias: 'rooms' });

  readonly controlNames = new BookingsFiltersFormHandler().getFormControlNames();
}
