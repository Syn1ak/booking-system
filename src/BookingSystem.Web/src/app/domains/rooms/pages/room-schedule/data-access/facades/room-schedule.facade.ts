import { HttpStatusCode } from '@angular/common/http';
import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { filter, finalize, interval, of, Subject, switchMap, tap, timeout } from 'rxjs';
import { IRoom } from '../../../../../../core/entities/rooms/room.dto';
import { IRoomSchedule, ISlot } from '../../../../../../core/entities/rooms/room-schedule.dto';
import { ISlotChange } from '../../../../../../core/entities/realtime/slot-change.dto';
import { injectHandleErrors } from '../../../../../../core/errors/handle-errors';
import { BookingsClient } from '../../../../../../core/services/api/bookings/bookings.client';
import { RoomsClient } from '../../../../../../core/services/api/rooms/rooms.client';
import { ToastService } from '../../../../../../core/services/notifications/toast.service';
import { ScheduleHubClient } from '../../../../../../core/services/realtime/schedule-hub.client';
import { todayUtc } from '../../../../../../core/utils/date/utc-date.util';
import { TSlotView } from '../../models/slot.types';
import {
  applySlotChange,
  mergeBookingResponse,
  mergeSnapshot,
  slotStatus,
} from '../../utils/slot-merge.util';

const JUST_CHANGED_MS = 1_600;
const CLOCK_TICK_MS = 30_000;
const WATCH_BEFORE_FETCH_TIMEOUT_MS = 4_000;

/**
 * A room's live schedule. The room is watched before its schedule is fetched, so no change can
 * fall between the snapshot and the subscription; events that arrive while a fetch is in flight
 * are buffered and replayed over the snapshot, and the sequence comparison keeps whichever is
 * newer. See .claude/realtime/realtime.md.
 */
@Injectable()
export class RoomScheduleFacade {
  private readonly roomsClient = inject(RoomsClient);
  private readonly bookingsClient = inject(BookingsClient);
  private readonly hub = inject(ScheduleHubClient);
  private readonly toasts = inject(ToastService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly handleErrors = injectHandleErrors();

  private readonly $roomIdState = signal<string | null>(null);
  private readonly $roomState = signal<IRoom | null>(null);
  private readonly $dateState = signal<string | null>(null);
  private readonly $lastBookableDateState = signal<string | null>(null);
  private readonly $slotsState = signal<ISlot[]>([]);
  private readonly $inFlight = signal(0);
  private readonly $pendingSlotIds = signal<ReadonlySet<string>>(new Set());
  private readonly $justChangedIds = signal<ReadonlySet<string>>(new Set());
  private readonly $now = signal(new Date());

  private readonly load$ = new Subject<void>();
  private bufferedChanges: ISlotChange[] = [];

  readonly $room = this.$roomState.asReadonly();
  readonly $date = this.$dateState.asReadonly();
  readonly $lastBookableDate = this.$lastBookableDateState.asReadonly();
  readonly $today = computed(() => todayUtc(this.$now()));
  readonly $loading = computed(() => this.$inFlight() > 0 && this.$slotsState().length === 0);
  readonly $refreshing = computed(() => this.$inFlight() > 0);

  readonly $slots = computed<TSlotView[]>(() => {
    const now = this.$now();
    const pending = this.$pendingSlotIds();
    const justChanged = this.$justChangedIds();

    return this.$slotsState().map((slot) => ({
      slot,
      status: slotStatus(slot, now),
      pending: pending.has(slot.slotId),
      justChanged: justChanged.has(slot.slotId),
    }));
  });

  readonly $availableCount = computed(
    () => this.$slots().filter((view) => view.status === 'free').length,
  );
  readonly $mineCount = computed(
    () => this.$slots().filter((view) => view.status === 'mine').length,
  );

  constructor() {
    this.load$
      .pipe(
        switchMap(() => this.fetchSchedule$()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((schedule) => this.applySnapshot(schedule));

    this.hub.slotChanged$
      .pipe(
        filter((change) => change.roomId === this.$roomIdState()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((change) => this.applyChange(change));

    this.hub.scheduleReset$
      .pipe(
        filter((roomId) => roomId === this.$roomIdState()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => this.reload());

    this.hub.reconnected$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.reload());

    interval(CLOCK_TICK_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.$now.set(new Date()));

    this.destroyRef.onDestroy(() => this.leaveRoom());
  }

  select(roomId: string, date: string | null): void {
    const requestedDate = date ?? todayUtc(this.$now());

    if (roomId !== this.$roomIdState()) {
      this.enterRoom(roomId, requestedDate);
      return;
    }

    if (requestedDate !== this.$dateState()) {
      this.$dateState.set(requestedDate);
      this.$slotsState.set([]);
      this.reload();
    }
  }

  reload(): void {
    if (this.$roomIdState()) {
      this.load$.next();
    }
  }

  book(slotId: string): void {
    if (this.$pendingSlotIds().has(slotId)) {
      return;
    }

    this.setPending(slotId, true);

    this.bookingsClient
      .bookSlot$({ slotId })
      .pipe(
        this.handleErrors({
          [HttpStatusCode.Conflict]: () => {
            this.toasts.warning(
              'Someone else just booked this slot',
              'Their request reached the server first. The schedule shows what is still free.',
            );
            this.reload();
          },
          [HttpStatusCode.BadRequest]: (problem) => {
            this.toasts.info(problem.title ?? 'This slot can no longer be booked', problem.detail);
            this.reload();
          },
          [HttpStatusCode.NotFound]: () => this.roomGone(),
        }),
        finalize(() => this.setPending(slotId, false)),
      )
      .subscribe((booking) => {
        this.$slotsState.update((slots) => mergeBookingResponse(slots, booking));
        this.toasts.success(
          'Slot booked',
          `It is yours in ${this.$roomState()?.name ?? 'this room'}.`,
        );
      });
  }

  cancel(slot: ISlot): void {
    if (!slot.myBookingId || this.$pendingSlotIds().has(slot.slotId)) {
      return;
    }

    this.setPending(slot.slotId, true);

    this.bookingsClient
      .cancelBooking$(slot.myBookingId)
      .pipe(
        this.handleErrors({
          [HttpStatusCode.BadRequest]: (problem) => {
            this.toasts.info(
              problem.title ?? 'This booking can no longer be cancelled',
              problem.detail,
            );
            this.reload();
          },
          [HttpStatusCode.NotFound]: () => this.reload(),
          [HttpStatusCode.Forbidden]: () => this.reload(),
        }),
        finalize(() => this.setPending(slot.slotId, false)),
      )
      .subscribe(() => {
        this.toasts.success('Booking cancelled', 'The slot is free for others again.');
        this.reload();
      });
  }

  private enterRoom(roomId: string, date: string): void {
    this.leaveRoom();

    this.$roomIdState.set(roomId);
    this.$roomState.set(null);
    this.$dateState.set(date);
    this.$slotsState.set([]);
    this.bufferedChanges = [];

    this.roomsClient
      .getRoom$(roomId)
      .pipe(
        this.handleErrors({ [HttpStatusCode.NotFound]: () => this.roomGone() }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((room) => this.$roomState.set(room));

    // Watching first is what closes the gap between the snapshot and the subscription. The
    // timeout only stops a slow hub from holding the schedule back; the pull stays authoritative.
    this.$inFlight.update((count) => count + 1);
    this.hub
      .watch$(roomId)
      .pipe(
        timeout({ first: WATCH_BEFORE_FETCH_TIMEOUT_MS, with: () => of(undefined) }),
        finalize(() => this.$inFlight.update((count) => count - 1)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({ complete: () => this.reload() });
  }

  private leaveRoom(): void {
    const roomId = this.$roomIdState();

    if (roomId) {
      this.hub.unwatch$(roomId).subscribe();
    }
  }

  private fetchSchedule$() {
    const roomId = this.$roomIdState() as string;
    const date = this.$dateState() ?? undefined;
    this.bufferedChanges = [];

    return of(null).pipe(
      tap(() => this.$inFlight.update((count) => count + 1)),
      switchMap(() => this.roomsClient.getSchedule$(roomId, date)),
      this.handleErrors<IRoomSchedule>({
        [HttpStatusCode.NotFound]: () => this.roomGone(),
        [HttpStatusCode.BadRequest]: (problem) => this.dateRefused(problem.detail),
      }),
      finalize(() => this.$inFlight.update((count) => count - 1)),
    );
  }

  private applySnapshot(schedule: IRoomSchedule): void {
    if (schedule.roomId !== this.$roomIdState() || schedule.date !== this.$dateState()) {
      return;
    }

    let slots = mergeSnapshot(this.$slotsState(), schedule.slots);

    for (const change of this.bufferedChanges) {
      const result = applySlotChange(slots, change, schedule.date);
      if (result.kind === 'applied') {
        slots = result.slots;
      }
    }

    this.bufferedChanges = [];
    this.$slotsState.set(slots);
    this.$lastBookableDateState.set(schedule.lastBookableDate ?? null);
  }

  private applyChange(change: ISlotChange): void {
    const date = this.$dateState();

    if (this.$inFlight() > 0) {
      this.bufferedChanges.push(change);
    }

    if (!date || this.$slotsState().length === 0) {
      return;
    }

    const result = applySlotChange(this.$slotsState(), change, date);

    if (result.kind === 'applied') {
      this.$slotsState.set(result.slots);
      this.flashChanged(change.slotId);
    } else if (result.kind === 'unreconcilable') {
      this.reload();
    }
  }

  private roomGone(): void {
    this.toasts.warning(
      'This room is no longer available',
      'It may have been removed by an administrator.',
    );
    void this.router.navigate(['/rooms']);
  }

  private dateRefused(detail: string | undefined): void {
    this.toasts.info('That date is not open for booking', detail);
    void this.router.navigate([], { queryParams: { date: null }, queryParamsHandling: 'merge' });
  }

  private setPending(slotId: string, pending: boolean): void {
    this.$pendingSlotIds.update((ids) => toggle(ids, slotId, pending));
  }

  private flashChanged(slotId: string): void {
    this.$justChangedIds.update((ids) => toggle(ids, slotId, true));
    setTimeout(
      () => this.$justChangedIds.update((ids) => toggle(ids, slotId, false)),
      JUST_CHANGED_MS,
    );
  }
}

function toggle(ids: ReadonlySet<string>, id: string, present: boolean): ReadonlySet<string> {
  const next = new Set(ids);

  if (present) {
    next.add(id);
  } else {
    next.delete(id);
  }

  return next;
}
