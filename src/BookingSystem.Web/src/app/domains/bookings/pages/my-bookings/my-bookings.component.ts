import { NgTemplateOutlet } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  Injector,
  OnInit,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  NgbModal,
  NgbNav,
  NgbNavContent,
  NgbNavItem,
  NgbNavLinkButton,
  NgbNavOutlet,
} from '@ng-bootstrap/ng-bootstrap';
import { openConfirmDialog } from '../../../../shared/ui/components/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../../../shared/ui/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../../shared/ui/components/page-header/page-header.component';
import { SkeletonComponent } from '../../../../shared/ui/components/skeleton/skeleton.component';
import { UtcTimePipe } from '../../../../shared/ui/pipes/utc-time.pipe';
import { MyBookingsFacade, TMyBookingView } from './data-access/facades/my-bookings.facade';
import { BookingListItemComponent } from './view/components/booking-list-item/booking-list-item.component';

@Component({
  selector: 'app-my-bookings',
  imports: [
    NgTemplateOutlet,
    RouterLink,
    NgbNav,
    NgbNavItem,
    NgbNavLinkButton,
    NgbNavContent,
    NgbNavOutlet,
    PageHeaderComponent,
    EmptyStateComponent,
    SkeletonComponent,
    BookingListItemComponent,
  ],
  templateUrl: './my-bookings.component.html',
  providers: [MyBookingsFacade, UtcTimePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class MyBookingsComponent implements OnInit {
  readonly placeholders = [1, 2, 3];

  readonly facade = inject(MyBookingsFacade);

  readonly tabs = [
    {
      id: 'upcoming',
      label: 'Upcoming',
      $views: this.facade.$upcoming,
      empty: 'No upcoming bookings. Find a free slot in any room.',
    },
    { id: 'past', label: 'Past', $views: this.facade.$past, empty: 'Nothing here yet.' },
    {
      id: 'cancelled',
      label: 'Cancelled',
      $views: this.facade.$cancelled,
      empty: 'You have not cancelled any bookings.',
    },
  ] as const;

  private readonly modals = inject(NgbModal);
  private readonly injector = inject(Injector);
  private readonly utcTime = inject(UtcTimePipe);

  readonly $activeTab = signal<string>('upcoming');

  ngOnInit(): void {
    this.facade.load();
  }

  async confirmCancel({ booking }: TMyBookingView): Promise<void> {
    const confirmed = await openConfirmDialog(this.modals, this.injector, {
      title: 'Cancel this booking?',
      message: `${this.utcTime.transform(booking.startsAtUtc)}–${this.utcTime.transform(booking.endsAtUtc)} UTC in ${booking.roomName} will be released for others to book.`,
      confirmLabel: 'Cancel booking',
      tone: 'danger',
    });

    if (confirmed) {
      this.facade.cancel(booking.bookingId);
    }
  }
}
