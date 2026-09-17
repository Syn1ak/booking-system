import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import {
  NgbDatepicker,
  NgbDateStruct,
  NgbDropdown,
  NgbDropdownMenu,
  NgbDropdownToggle,
} from '@ng-bootstrap/ng-bootstrap';
import {
  addDays,
  fromDateParts,
  toDateParts,
} from '../../../../../../../core/utils/date/utc-date.util';
import { UtcDatePipe } from '../../../../../../../shared/ui/pipes/utc-date.pipe';

/** Only until the first schedule response names the real window. */
const DAYS_SHOWN_WITHOUT_WINDOW = 365;

@Component({
  selector: 'app-date-navigator',
  imports: [NgbDatepicker, NgbDropdown, NgbDropdownToggle, NgbDropdownMenu, UtcDatePipe],
  template: `
    <div class="d-flex flex-wrap align-items-center gap-2" role="group" aria-label="Choose a date">
      <div class="btn-group">
        <button
          type="button"
          class="btn btn-outline-secondary"
          aria-label="Previous day"
          [disabled]="!$canGoBack()"
          (click)="$dateChange.emit(shift(-1))"
        >
          <i class="bi bi-chevron-left" aria-hidden="true"></i>
        </button>
        <div ngbDropdown #dropdown="ngbDropdown" class="btn-group" placement="bottom-start">
          <button
            type="button"
            class="btn btn-outline-secondary fw-semibold px-3"
            ngbDropdownToggle
          >
            <i class="bi bi-calendar3 me-2" aria-hidden="true"></i>{{ $date() | utcDate: 'medium' }}
          </button>
          <div ngbDropdownMenu class="p-2 border-0 shadow">
            <ngb-datepicker
              [startDate]="$selected()"
              [minDate]="$min()"
              [maxDate]="$max()"
              [markDisabled]="isOutsideWindow"
              (dateSelect)="select($event); dropdown.close()"
            />
          </div>
        </div>
        <button
          type="button"
          class="btn btn-outline-secondary"
          aria-label="Next day"
          [disabled]="!$canGoForward()"
          (click)="$dateChange.emit(shift(1))"
        >
          <i class="bi bi-chevron-right" aria-hidden="true"></i>
        </button>
      </div>
      <button
        type="button"
        class="btn btn-link text-decoration-none"
        [disabled]="$date() === $today()"
        (click)="$dateChange.emit($today())"
      >
        Today
      </button>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DateNavigatorComponent {
  $date = input.required<string>({ alias: 'date' });
  $today = input.required<string>({ alias: 'today' });
  $lastBookableDate = input<string | null>(null, { alias: 'lastBookableDate' });
  $dateChange = output<string>({ alias: 'dateChange' });

  readonly $selected = computed(() => toDateParts(this.$date()));
  readonly $min = computed(() => toDateParts(this.$today()));
  readonly $max = computed(() => {
    const last = this.$lastBookableDate();
    const today = this.$today();
    return toDateParts(last ?? addDays(today, DAYS_SHOWN_WITHOUT_WINDOW));
  });
  readonly $canGoBack = computed(() => this.$date() > this.$today());
  readonly $canGoForward = computed(() => {
    const last = this.$lastBookableDate();
    const date = this.$date();
    return !last || date < last;
  });

  readonly isOutsideWindow = (date: NgbDateStruct): boolean => {
    const value = fromDateParts(date);
    const last = this.$lastBookableDate();
    return value < this.$today() || (!!last && value > last);
  };

  shift(days: number): string {
    return addDays(this.$date(), days);
  }

  select(date: NgbDateStruct): void {
    this.$dateChange.emit(fromDateParts(date));
  }
}
