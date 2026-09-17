import { TSlotStatus } from '../models/slot.types';

export const SLOT_STATUS_PRESENTATION: Record<
  TSlotStatus,
  { label: string; icon: string; badgeClass: string }
> = {
  free: {
    label: 'Available',
    icon: 'bi-circle',
    badgeClass: 'bg-success-subtle text-success-emphasis',
  },
  booked: {
    label: 'Booked',
    icon: 'bi-lock-fill',
    badgeClass: 'bg-body-secondary text-body-secondary',
  },
  mine: {
    label: 'Your booking',
    icon: 'bi-person-check-fill',
    badgeClass: 'bg-primary-subtle text-primary-emphasis',
  },
  past: {
    label: 'Past',
    icon: 'bi-clock-history',
    badgeClass: 'bg-body-tertiary text-body-tertiary',
  },
};
