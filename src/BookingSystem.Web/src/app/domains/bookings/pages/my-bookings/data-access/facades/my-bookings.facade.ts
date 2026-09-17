import { HttpStatusCode } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { finalize, forkJoin } from 'rxjs';
import { IMyBooking } from '../../../../../../core/entities/bookings/my-booking.dto';
import { injectHandleErrors } from '../../../../../../core/errors/handle-errors.util';
import { BookingsClient } from '../../../../../../core/services/api/bookings/bookings.client';
import { RoomsClient } from '../../../../../../core/services/api/rooms/rooms.client';
import { ToastService } from '../../../../../../core/services/notifications/toast.service';
import { TBookingStatus } from '../../../../models/booking-status.types';
import { bookingStatus } from '../../../../utils/booking-status.util';

export type TMyBookingView = {
  booking: IMyBooking;
  status: TBookingStatus;
  roomIsActive: boolean;
  pending: boolean;
};

@Injectable()
export class MyBookingsFacade {
  private readonly bookingsClient = inject(BookingsClient);
  private readonly roomsClient = inject(RoomsClient);
  private readonly toasts = inject(ToastService);
  private readonly handleErrors = injectHandleErrors();

  private readonly $bookings = signal<IMyBooking[]>([]);
  private readonly $activeRoomIds = signal<ReadonlySet<string>>(new Set());
  private readonly $pendingId = signal<string | null>(null);
  private readonly $loadingState = signal(true);

  readonly $loading = this.$loadingState.asReadonly();

  private readonly $views = computed<TMyBookingView[]>(() => {
    const now = new Date();
    const activeRoomIds = this.$activeRoomIds();
    const pendingId = this.$pendingId();

    return this.$bookings().map((booking) => ({
      booking,
      status: bookingStatus(booking, now),
      roomIsActive: activeRoomIds.has(booking.roomId),
      pending: booking.bookingId === pendingId,
    }));
  });

  readonly $upcoming = computed(() =>
    this.$views()
      .filter((view) => view.status === 'upcoming')
      .reverse(),
  );
  readonly $past = computed(() => this.$views().filter((view) => view.status === 'past'));
  readonly $cancelled = computed(() => this.$views().filter((view) => view.status === 'cancelled'));

  load(): void {
    this.$loadingState.set(true);

    forkJoin([this.bookingsClient.getMyBookings$(), this.roomsClient.getRooms$()])
      .pipe(
        this.handleErrors(),
        finalize(() => this.$loadingState.set(false)),
      )
      .subscribe(([bookings, rooms]) => {
        this.$bookings.set(bookings);
        this.$activeRoomIds.set(new Set(rooms.map((room) => room.roomId)));
      });
  }

  cancel(bookingId: string): void {
    this.$pendingId.set(bookingId);

    this.bookingsClient
      .cancelBooking$(bookingId)
      .pipe(
        this.handleErrors({
          [HttpStatusCode.BadRequest]: (problem) =>
            this.toasts.info(
              problem.title ?? 'This booking can no longer be cancelled',
              problem.detail,
            ),
        }),
        finalize(() => {
          this.$pendingId.set(null);
          this.load();
        }),
      )
      .subscribe(() =>
        this.toasts.success('Booking cancelled', 'The slot is free for others again.'),
      );
  }
}
