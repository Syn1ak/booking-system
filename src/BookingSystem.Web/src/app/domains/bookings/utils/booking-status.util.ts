import { IMyBooking } from '../../../core/entities/bookings/my-booking.dto';
import { TBookingStatus } from '../models/booking-status.types';

export function bookingStatus(booking: IMyBooking, now: Date): TBookingStatus {
  if (booking.cancelledAtUtc) {
    return 'cancelled';
  }

  return new Date(booking.startsAtUtc).getTime() > now.getTime() ? 'upcoming' : 'past';
}
