import { IMyBooking } from '../../../core/entities/bookings/my-booking.dto';
import { bookingStatus } from './booking-status.util';

describe('bookingStatus', () => {
  const now = new Date('2026-09-17T10:00:00Z');
  const booking = (overrides: Partial<IMyBooking>): IMyBooking => ({
    bookingId: 'b1',
    slotId: 's1',
    roomId: 'r1',
    roomName: 'Board room',
    startsAtUtc: '2026-09-17T11:00:00Z',
    endsAtUtc: '2026-09-17T12:00:00Z',
    createdAtUtc: '2026-09-16T08:00:00Z',
    cancelledAtUtc: null,
    ...overrides,
  });

  it('is upcoming until the slot starts, then past', () => {
    expect(bookingStatus(booking({}), now)).toBe('upcoming');
    expect(bookingStatus(booking({ startsAtUtc: '2026-09-17T10:00:00Z' }), now)).toBe('past');
  });

  it('is cancelled regardless of time', () => {
    expect(bookingStatus(booking({ cancelledAtUtc: '2026-09-16T09:00:00Z' }), now)).toBe(
      'cancelled',
    );
  });
});
