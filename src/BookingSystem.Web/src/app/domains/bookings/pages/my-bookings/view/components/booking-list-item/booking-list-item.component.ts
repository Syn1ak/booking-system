import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UtcDatePipe } from '../../../../../../../shared/ui/pipes/utc-date.pipe';
import { UtcTimePipe } from '../../../../../../../shared/ui/pipes/utc-time.pipe';
import { BookingStatusBadgeComponent } from '../../../../../view/components/booking-status-badge/booking-status-badge.component';
import { TMyBookingView } from '../../../data-access/facades/my-bookings.facade';

@Component({
  selector: 'app-booking-list-item',
  imports: [RouterLink, UtcDatePipe, UtcTimePipe, BookingStatusBadgeComponent],
  template: `
    <div class="list-group-item d-flex flex-wrap align-items-center gap-3 py-3">
      <div
        class="text-center rounded-3 bg-body-tertiary border px-3 py-2 flex-shrink-0"
        style="min-width: 4.25rem"
      >
        <div class="small text-uppercase fw-semibold text-body-secondary">
          {{ $view().booking.startsAtUtc | utcDate: 'month' }}
        </div>
        <div class="fs-4 fw-bold lh-1">{{ $view().booking.startsAtUtc | utcDate: 'day' }}</div>
      </div>

      <div class="flex-grow-1" style="min-width: 12rem">
        <div class="fw-semibold">
          @if ($view().roomIsActive) {
            <a
              [routerLink]="['/rooms', $view().booking.roomId]"
              [queryParams]="{ date: $view().booking.startsAtUtc.slice(0, 10) }"
              >{{ $view().booking.roomName }}</a
            >
          } @else {
            {{ $view().booking.roomName }}
            <span class="small text-body-secondary fw-normal">(room no longer available)</span>
          }
        </div>
        <div class="small text-body-secondary">
          {{ $view().booking.startsAtUtc | utcDate: 'weekday' }} ·
          {{ $view().booking.startsAtUtc | utcTime }}–{{ $view().booking.endsAtUtc | utcTime }} UTC
        </div>
      </div>

      <app-booking-status-badge [status]="$view().status" />

      @if ($view().status === 'upcoming') {
        <button
          type="button"
          class="btn btn-sm btn-outline-danger"
          [disabled]="$view().pending"
          (click)="$cancelBooking.emit($view())"
        >
          @if ($view().pending) {
            <span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>
          }
          Cancel
        </button>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingListItemComponent {
  $view = input.required<TMyBookingView>({ alias: 'view' });
  $cancelBooking = output<TMyBookingView>({ alias: 'cancelBooking' });
}
