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
import { Router, RouterLink } from '@angular/router';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { openConfirmDialog } from '../../../../shared/ui/components/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../../../shared/ui/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../../shared/ui/components/page-header/page-header.component';
import { SkeletonComponent } from '../../../../shared/ui/components/skeleton/skeleton.component';
import { TimeOnlyPipe } from '../../../../shared/ui/pipes/time-only.pipe';
import { UtcDatePipe } from '../../../../shared/ui/pipes/utc-date.pipe';
import { UtcTimePipe } from '../../../../shared/ui/pipes/utc-time.pipe';
import { SLOT_STATUS_PRESENTATION } from './constants/slot-status.constant';
import { RoomScheduleFacade } from './data-access/facades/room-schedule.facade';
import { TSlotView } from './models/slot.types';
import { DateNavigatorComponent } from './view/components/date-navigator/date-navigator.component';
import { SlotTileComponent } from './view/components/slot-tile/slot-tile.component';

@Component({
  selector: 'app-room-schedule',
  imports: [
    RouterLink,
    PageHeaderComponent,
    EmptyStateComponent,
    SkeletonComponent,
    DateNavigatorComponent,
    SlotTileComponent,
    TimeOnlyPipe,
    UtcDatePipe,
  ],
  templateUrl: './room-schedule.component.html',
  providers: [RoomScheduleFacade, UtcTimePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export default class RoomScheduleComponent {
  readonly legend = (['free', 'mine', 'booked'] as const).map((status) => ({
    status,
    ...SLOT_STATUS_PRESENTATION[status],
  }));
  readonly placeholders = Array.from({ length: 8 }, (_, index) => index);

  readonly facade = inject(RoomScheduleFacade);
  private readonly router = inject(Router);
  private readonly modals = inject(NgbModal);
  private readonly injector = inject(Injector);
  private readonly utcTime = inject(UtcTimePipe);
  private readonly destroyRef = inject(DestroyRef);

  $roomId = input.required<string>({ alias: 'roomId' });
  $dateParam = input<string | null>(null, { alias: 'date' });

  private readonly $selection = computed(() => ({
    roomId: this.$roomId(),
    date: this.$dateParam(),
  }));

  constructor() {
    toObservable(this.$selection)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ roomId, date }) => this.facade.select(roomId, date));
  }

  changeDate(date: string): void {
    void this.router.navigate([], {
      queryParams: { date: date === this.facade.$today() ? null : date },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  async confirmCancel({ slot }: TSlotView): Promise<void> {
    const confirmed = await openConfirmDialog(this.modals, this.injector, {
      title: 'Cancel this booking?',
      message: `${this.utcTime.transform(slot.startsAtUtc)}–${this.utcTime.transform(slot.endsAtUtc)} UTC in ${this.facade.$room()?.name ?? 'this room'} will be released for others to book.`,
      confirmLabel: 'Cancel booking',
      tone: 'danger',
    });

    if (confirmed) {
      this.facade.cancel(slot);
    }
  }
}
