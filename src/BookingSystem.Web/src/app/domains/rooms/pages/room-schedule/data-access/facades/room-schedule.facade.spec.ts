import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { defer, Subject } from 'rxjs';
import { IRoomSchedule, ISlot } from '../../../../../../core/entities/rooms/room-schedule.dto';
import { ISlotChange } from '../../../../../../core/entities/realtime/slot-change.dto';
import { ToastService } from '../../../../../../core/services/notifications/toast.service';
import { ScheduleHubClient } from '../../../../../../core/services/realtime/schedule-hub.client';
import { addDays, todayUtc } from '../../../../../../core/utils/date/utc-date.util';
import { RoomScheduleFacade } from './room-schedule.facade';

const ROOM_ID = 'r1';
const DATE = addDays(todayUtc(), 1);

const slot = (overrides: Partial<ISlot> = {}): ISlot => ({
  slotId: 's1',
  startsAtUtc: `${DATE}T09:00:00Z`,
  endsAtUtc: `${DATE}T10:00:00Z`,
  isBooked: false,
  myBookingId: null,
  sequence: 10,
  ...overrides,
});

const schedule = (slots: ISlot[]): IRoomSchedule => ({
  roomId: ROOM_ID,
  date: DATE,
  lastBookableDate: addDays(DATE, 13),
  slots,
});

describe('RoomScheduleFacade', () => {
  let http: HttpTestingController;
  let facade: RoomScheduleFacade;
  let calls: string[];
  let hub: {
    slotChanged$: Subject<ISlotChange>;
    scheduleReset$: Subject<string>;
    reconnected$: Subject<void>;
    watch$: ReturnType<typeof vi.fn>;
    unwatch$: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    calls = [];
    hub = {
      slotChanged$: new Subject(),
      scheduleReset$: new Subject(),
      reconnected$: new Subject(),
      watch$: vi.fn(() =>
        defer(async () => {
          calls.push('watch');
        }),
      ),
      unwatch$: vi.fn(() => defer(async () => undefined)),
    };

    TestBed.configureTestingModule({
      providers: [
        RoomScheduleFacade,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ScheduleHubClient, useValue: hub },
      ],
    });

    http = TestBed.inject(HttpTestingController);
    facade = TestBed.inject(RoomScheduleFacade);
  });

  const scheduleRequest = () =>
    http.expectOne((request) => request.url === `/api/rooms/${ROOM_ID}/schedule`);

  const open = async (slots: ISlot[] = [slot()]) => {
    facade.select(ROOM_ID, DATE);
    http.expectOne(`/api/rooms/${ROOM_ID}`).flush({
      roomId: ROOM_ID,
      name: 'Board room',
      opensAtUtc: '09:00:00',
      closesAtUtc: '17:00:00',
      slotLengthMinutes: 60,
    });
    await vi.waitFor(() => expect(calls).toContain('watch'));
    const request = scheduleRequest();
    calls.push('fetch');
    request.flush(schedule(slots));
  };

  it('joins the room before fetching its schedule', async () => {
    let joined!: () => void;
    hub.watch$.mockReturnValueOnce(defer(() => new Promise<void>((resolve) => (joined = resolve))));

    facade.select(ROOM_ID, DATE);
    await vi.waitFor(() => expect(joined).toBeDefined());

    http.expectNone((request) => request.url === `/api/rooms/${ROOM_ID}/schedule`);

    joined();
    await vi.waitFor(() => scheduleRequest().flush(schedule([slot()])));
    expect(facade.$slots()[0].status).toBe('free');
  });

  it('keeps an event that arrives while the schedule is being fetched', async () => {
    facade.select(ROOM_ID, DATE);
    http.expectOne(`/api/rooms/${ROOM_ID}`).flush(null, { status: 200, statusText: 'OK' });
    await vi.waitFor(() => expect(hub.watch$).toHaveBeenCalled());
    const request = await vi.waitFor(() => scheduleRequest());

    hub.slotChanged$.next({
      roomId: ROOM_ID,
      slotId: 's1',
      startsAtUtc: `${DATE}T09:00:00Z`,
      isBooked: true,
      sequence: 11,
    });
    request.flush(schedule([slot({ isBooked: false, sequence: 10 })]));

    expect(facade.$slots()[0].status).toBe('booked');
  });

  it('shows the booking as the caller’s own from the response', async () => {
    await open();

    facade.book('s1');
    http.expectOne('/api/bookings').flush(
      {
        bookingId: 'b1',
        slotId: 's1',
        roomId: ROOM_ID,
        startsAtUtc: `${DATE}T09:00:00Z`,
        endsAtUtc: `${DATE}T10:00:00Z`,
        createdAtUtc: `${DATE}T08:00:00Z`,
        sequence: 11,
      },
      { status: 201, statusText: 'Created' },
    );

    expect(facade.$slots()[0].status).toBe('mine');
  });

  it('treats a lost race as a warning and refetches, never as an error', async () => {
    await open();
    const toasts = TestBed.inject(ToastService);

    facade.book('s1');
    http
      .expectOne('/api/bookings')
      .flush({ title: 'Slot already booked' }, { status: 409, statusText: 'Conflict' });

    expect(toasts.$items().map((toast) => toast.tone)).toEqual(['warning']);
    scheduleRequest().flush(schedule([slot({ isBooked: true, sequence: 11 })]));
    expect(facade.$slots()[0].status).toBe('booked');
  });

  it('refetches instead of leaving the room when the slot has left the grid', async () => {
    await open();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    facade.book('s1');
    http.expectOne('/api/bookings').flush(null, { status: 404, statusText: 'Not Found' });

    scheduleRequest().flush(schedule([slot({ slotId: 's2', sequence: 30 })]));
    expect(navigate).not.toHaveBeenCalled();
    expect(facade.$slots().map((view) => view.slot.slotId)).toEqual(['s2']);
  });

  it('refetches on a schedule reset and leaves a room that no longer exists', async () => {
    await open();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    hub.scheduleReset$.next(ROOM_ID);
    scheduleRequest().flush(null, { status: 404, statusText: 'Not Found' });

    expect(navigate).toHaveBeenCalledWith(['/rooms']);
  });

  it('refetches after the hub reconnects, because events during the gap are gone', async () => {
    await open();

    hub.reconnected$.next();

    scheduleRequest().flush(schedule([slot({ isBooked: true, sequence: 14 })]));
    expect(facade.$slots()[0].status).toBe('booked');
  });

  it('refetches when an event names a slot it does not know', async () => {
    await open();

    hub.slotChanged$.next({
      roomId: ROOM_ID,
      slotId: 'new-grid-slot',
      startsAtUtc: `${DATE}T09:30:00Z`,
      isBooked: true,
      sequence: 20,
    });

    expect(scheduleRequest()).toBeTruthy();
  });
});
