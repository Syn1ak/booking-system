import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { BOOKING_STATUS_PRESENTATION } from '../../../constants/booking-status.constant';
import { TBookingStatus } from '../../../models/booking-status.types';

@Component({
  selector: 'app-booking-status-badge',
  template: `
    <span class="badge d-inline-flex align-items-center gap-1" [class]="$presentation().badgeClass">
      <i class="bi" [class]="$presentation().icon" aria-hidden="true"></i>
      {{ $presentation().label }}
    </span>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BookingStatusBadgeComponent {
  $status = input.required<TBookingStatus>({ alias: 'status' });

  readonly $presentation = computed(() => BOOKING_STATUS_PRESENTATION[this.$status()]);
}
