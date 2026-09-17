import { HttpStatusCode } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, Subject, switchMap } from 'rxjs';
import { IAdminBooking } from '../../../../../../core/entities/bookings/admin-booking.dto';
import { IAdminBookingsCriteria } from '../../../../../../core/entities/bookings/admin-bookings.criteria';
import { IRoom } from '../../../../../../core/entities/rooms/room.dto';
import { injectHandleErrors } from '../../../../../../core/errors/handle-errors';
import { BookingsClient } from '../../../../../../core/services/api/bookings/bookings.client';
import { RoomsClient } from '../../../../../../core/services/api/rooms/rooms.client';
import { ToastService } from '../../../../../../core/services/notifications/toast.service';
import { TBookingStatus } from '../../../../models/booking-status.types';
import { bookingStatus } from '../../../../utils/booking-status.util';
import { PAGE_SIZE, TBookingsSortKey } from '../../constants/bookings-table.constant';

export type TAdminBookingView = {
  booking: IAdminBooking;
  status: TBookingStatus;
  pending: boolean;
};

export type TSortDirection = 'asc' | 'desc';

@Injectable()
export class AllBookingsFacade {
  private readonly bookingsClient = inject(BookingsClient);
  private readonly roomsClient = inject(RoomsClient);
  private readonly toasts = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly handleErrors = injectHandleErrors();

  private readonly $bookings = signal<IAdminBooking[]>([]);
  private readonly $roomsState = signal<IRoom[]>([]);
  private readonly $loadingState = signal(true);
  private readonly $pendingId = signal<string | null>(null);
  private readonly $sortState = signal<{ key: TBookingsSortKey; direction: TSortDirection }>({
    key: 'startsAtUtc',
    direction: 'desc',
  });
  private readonly $pageState = signal(1);

  private readonly criteria$ = new Subject<IAdminBookingsCriteria>();
  private lastCriteria: IAdminBookingsCriteria = {};

  readonly pageSize = PAGE_SIZE;
  readonly $rooms = this.$roomsState.asReadonly();
  readonly $loading = this.$loadingState.asReadonly();
  readonly $sort = this.$sortState.asReadonly();
  readonly $page = this.$pageState.asReadonly();

  private readonly $sorted = computed<TAdminBookingView[]>(() => {
    const now = new Date();
    const pendingId = this.$pendingId();
    const { key, direction } = this.$sortState();
    const factor = direction === 'asc' ? 1 : -1;

    return this.$bookings()
      .map((booking) => ({
        booking,
        status: bookingStatus(booking, now),
        pending: booking.bookingId === pendingId,
      }))
      .sort((a, b) => factor * (a.booking[key] ?? '').localeCompare(b.booking[key] ?? ''));
  });

  readonly $total = computed(() => this.$sorted().length);
  readonly $liveCount = computed(
    () => this.$sorted().filter((view) => view.status !== 'cancelled').length,
  );
  readonly $pageItems = computed(() => {
    const start = (this.$pageState() - 1) * PAGE_SIZE;
    return this.$sorted().slice(start, start + PAGE_SIZE);
  });

  constructor() {
    this.criteria$
      .pipe(
        switchMap((criteria) => {
          this.$loadingState.set(true);
          return this.bookingsClient.getAllBookings$(criteria).pipe(
            this.handleErrors(),
            finalize(() => this.$loadingState.set(false)),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((bookings) => {
        this.$bookings.set(bookings);
        this.$pageState.set(1);
      });

    this.roomsClient
      .getRooms$()
      .pipe(this.handleErrors(), takeUntilDestroyed(this.destroyRef))
      .subscribe((rooms) => this.$roomsState.set(rooms));
  }

  load(criteria: IAdminBookingsCriteria): void {
    this.lastCriteria = criteria;
    this.criteria$.next(criteria);
  }

  sortBy(key: TBookingsSortKey): void {
    this.$sortState.update((sort) => ({
      key,
      direction: sort.key === key && sort.direction === 'asc' ? 'desc' : 'asc',
    }));
  }

  goToPage(page: number): void {
    this.$pageState.set(page);
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
          this.load(this.lastCriteria);
        }),
      )
      .subscribe(() => this.toasts.success('Booking cancelled', 'The slot is free again.'));
  }
}
