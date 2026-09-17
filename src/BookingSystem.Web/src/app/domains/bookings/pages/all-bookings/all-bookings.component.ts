import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  Injector,
  input,
} from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { NgbModal, NgbPagination } from '@ng-bootstrap/ng-bootstrap';
import { debounceTime, filter } from 'rxjs';
import { SessionService } from '../../../../core/services/session/session.service';
import { openConfirmDialog } from '../../../../shared/ui/components/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../../../shared/ui/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../../shared/ui/components/page-header/page-header.component';
import { SkeletonComponent } from '../../../../shared/ui/components/skeleton/skeleton.component';
import { UtcDatePipe } from '../../../../shared/ui/pipes/utc-date.pipe';
import { UtcTimePipe } from '../../../../shared/ui/pipes/utc-time.pipe';
import { BookingStatusBadgeComponent } from '../../view/components/booking-status-badge/booking-status-badge.component';
import { BOOKINGS_COLUMNS } from './constants/bookings-table.constant';
import { AllBookingsFacade, TAdminBookingView } from './data-access/facades/all-bookings.facade';
import { BookingsFiltersFormHandler } from './models/bookings-filters.form';
import { BookingsFiltersComponent } from './view/components/bookings-filters/bookings-filters.component';

const FILTER_DEBOUNCE_MS = 300;

@Component({
  selector: 'app-all-bookings',
  imports: [
    NgbPagination,
    PageHeaderComponent,
    EmptyStateComponent,
    SkeletonComponent,
    BookingStatusBadgeComponent,
    BookingsFiltersComponent,
    UtcDatePipe,
    UtcTimePipe,
  ],
  templateUrl: './all-bookings.component.html',
  providers: [AllBookingsFacade, UtcTimePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class AllBookingsComponent {
  readonly columns = BOOKINGS_COLUMNS;
  readonly placeholders = Array.from({ length: 6 }, (_, index) => index);

  readonly facade = inject(AllBookingsFacade);
  readonly session = inject(SessionService);
  private readonly router = inject(Router);
  private readonly modals = inject(NgbModal);
  private readonly injector = inject(Injector);
  private readonly utcTime = inject(UtcTimePipe);
  private readonly destroyRef = inject(DestroyRef);

  $roomId = input<string | null>(null, { alias: 'roomId' });
  $from = input<string | null>(null, { alias: 'from' });
  $to = input<string | null>(null, { alias: 'to' });
  $includeCancelled = input<string | null>(null, { alias: 'includeCancelled' });

  private readonly $criteria = computed(() =>
    BookingsFiltersFormHandler.toCriteria({
      roomId: this.$roomId(),
      from: this.$from(),
      to: this.$to(),
      includeCancelled: this.$includeCancelled(),
    }),
  );

  readonly filters = new BookingsFiltersFormHandler();

  constructor() {
    toObservable(this.$criteria)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((criteria) => {
        this.filters.setFromCriteria(criteria);
        this.facade.load(criteria);
      });

    this.filters.valueChanges
      .pipe(
        debounceTime(FILTER_DEBOUNCE_MS),
        filter(() => this.filters.valid),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() =>
        this.router.navigate([], { queryParams: this.filters.toQueryParams(), replaceUrl: true }),
      );
  }

  async confirmCancel({ booking }: TAdminBookingView): Promise<void> {
    const confirmed = await openConfirmDialog(this.modals, this.injector, {
      title: 'Cancel this booking?',
      message: `${booking.userEmail ?? 'This user'} loses ${this.utcTime.transform(booking.startsAtUtc)}–${this.utcTime.transform(booking.endsAtUtc)} UTC in ${booking.roomName}. They are not notified.`,
      confirmLabel: 'Cancel booking',
      tone: 'danger',
    });

    if (confirmed) {
      this.facade.cancel(booking.bookingId);
    }
  }
}
