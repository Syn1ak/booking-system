import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { NgbTooltip } from '@ng-bootstrap/ng-bootstrap';
import { LocalTimePipe } from '../../../../../../../shared/ui/pipes/local-time.pipe';
import { UtcTimePipe } from '../../../../../../../shared/ui/pipes/utc-time.pipe';
import { SLOT_STATUS_PRESENTATION } from '../../../constants/slot-status.constant';
import { TSlotView } from '../../../models/slot.types';

@Component({
  selector: 'app-slot-tile',
  imports: [NgbTooltip, UtcTimePipe, LocalTimePipe],
  template: `
    <div
      class="slot-tile h-100"
      [class]="'slot-tile--' + $view().status"
      [class.slot-tile--just-changed]="$view().justChanged"
    >
      <div class="d-flex justify-content-between align-items-start gap-2">
        <div>
          <div
            class="slot-time"
            [ngbTooltip]="'Your time: ' + ($view().slot.startsAtUtc | localTime)"
            tabindex="0"
          >
            {{ $view().slot.startsAtUtc | utcTime }}
          </div>
          <div class="small text-body-secondary">until {{ $view().slot.endsAtUtc | utcTime }}</div>
        </div>
        <span
          class="badge d-inline-flex align-items-center gap-1"
          [class]="$presentation().badgeClass"
        >
          <i class="bi" [class]="$presentation().icon" aria-hidden="true"></i>
          {{ $presentation().label }}
        </span>
      </div>

      @switch ($view().status) {
        @case ('free') {
          <button
            type="button"
            class="btn btn-success btn-sm w-100 mt-auto"
            [disabled]="$view().pending"
            (click)="$book.emit($view().slot.slotId)"
          >
            @if ($view().pending) {
              <span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>
              Booking…
            } @else {
              <i class="bi bi-plus-lg me-1" aria-hidden="true"></i> Book
            }
          </button>
        }
        @case ('mine') {
          <button
            type="button"
            class="btn btn-outline-danger btn-sm w-100 mt-auto"
            [disabled]="$view().pending"
            (click)="$cancel.emit($view())"
          >
            @if ($view().pending) {
              <span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>
              Cancelling…
            } @else {
              <i class="bi bi-x-lg me-1" aria-hidden="true"></i> Cancel booking
            }
          </button>
        }
        @case ('booked') {
          <div class="small text-body-secondary mt-auto py-1">Taken by another attendee</div>
        }
        @default {
          <div class="small text-body-tertiary mt-auto py-1">Already started</div>
        }
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SlotTileComponent {
  $view = input.required<TSlotView>({ alias: 'view' });
  $book = output<string>({ alias: 'bookSlot' });
  $cancel = output<TSlotView>({ alias: 'cancelBooking' });

  readonly $presentation = computed(() => SLOT_STATUS_PRESENTATION[this.$view().status]);
}
