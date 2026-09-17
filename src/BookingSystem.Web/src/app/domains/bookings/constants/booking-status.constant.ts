import { TBookingStatus } from '../models/booking-status.types';

export const BOOKING_STATUS_PRESENTATION: Record<
  TBookingStatus,
  { label: string; icon: string; badgeClass: string }
> = {
  upcoming: {
    label: 'Upcoming',
    icon: 'bi-calendar-check',
    badgeClass: 'bg-primary-subtle text-primary-emphasis',
  },
  past: {
    label: 'Past',
    icon: 'bi-clock-history',
    badgeClass: 'bg-body-secondary text-body-secondary',
  },
  cancelled: {
    label: 'Cancelled',
    icon: 'bi-x-circle',
    badgeClass: 'bg-danger-subtle text-danger-emphasis',
  },
};
