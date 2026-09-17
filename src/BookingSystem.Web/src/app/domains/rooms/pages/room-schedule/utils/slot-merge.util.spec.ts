import { IBooking } from '../../../../../core/entities/bookings/booking.dto';
import { ISlot } from '../../../../../core/entities/rooms/room-schedule.dto';
import { ISlotChange } from '../../../../../core/entities/realtime/slot-change.dto';
import {
  applySlotChange,
  mergeBookingResponse,
  mergeSnapshot,
  slotStatus,
} from './slot-merge.util';

const DATE = '2026-09-18';

const slot = (overrides: Partial<ISlot> = {}): ISlot => ({
  slotId: 's1',
  startsAtUtc: `${DATE}T09:00:00Z`,
  endsAtUtc: `${DATE}T10:00:00Z`,
  isBooked: false,
  myBookingId: null,
  sequence: 10,
  ...overrides,
});

const change = (overrides: Partial<ISlotChange> = {}): ISlotChange => ({
  roomId: 'r1',
  slotId: 's1',
  startsAtUtc: `${DATE}T09:00:00Z`,
  isBooked: true,
  sequence: 11,
  ...overrides,
});

const booking = (overrides: Partial<IBooking> = {}): IBooking => ({
  bookingId: 'b1',
  slotId: 's1',
  roomId: 'r1',
  startsAtUtc: `${DATE}T09:00:00Z`,
  endsAtUtc: `${DATE}T10:00:00Z`,
  createdAtUtc: `${DATE}T08:00:00Z`,
  sequence: 11,
  ...overrides,
});

describe('applySlotChange', () => {
  it('ignores an event for a date other than the one being viewed', () => {
    const result = applySlotChange([slot()], change({ startsAtUtc: '2026-09-19T09:00:00Z' }), DATE);

    expect(result.kind).toBe('ignored');
  });

  it('ignores an event no newer than what is held, so out-of-order delivery cannot regress a slot', () => {
    const held = [slot({ isBooked: false, sequence: 12 })];

    expect(applySlotChange(held, change({ isBooked: true, sequence: 11 }), DATE).kind).toBe(
      'ignored',
    );
    expect(applySlotChange(held, change({ isBooked: true, sequence: 12 }), DATE).kind).toBe(
      'ignored',
    );
  });

  it('replaces isBooked from a newer event', () => {
    const result = applySlotChange([slot()], change({ isBooked: true, sequence: 11 }), DATE);

    expect(result).toEqual({
      kind: 'applied',
      slots: [slot({ isBooked: true, sequence: 11 })],
    });
  });

  it('clears myBookingId on any newer event, because the slot has changed since it was ours', () => {
    const held = [slot({ isBooked: true, myBookingId: 'b1', sequence: 11 })];

    const result = applySlotChange(held, change({ isBooked: false, sequence: 12 }), DATE);

    expect(result.kind).toBe('applied');
    expect(result.kind === 'applied' && result.slots[0].myBookingId).toBeNull();
  });

  it('keeps myBookingId when the event is the caller’s own booking, already applied', () => {
    const held = [slot({ isBooked: true, myBookingId: 'b1', sequence: 11 })];

    expect(applySlotChange(held, change({ sequence: 11 }), DATE).kind).toBe('ignored');
  });

  it('reports an event for an unknown slot on the viewed date as unreconcilable', () => {
    const result = applySlotChange([slot()], change({ slotId: 'unknown' }), DATE);

    expect(result.kind).toBe('unreconcilable');
  });

  it('never produces a holder: an applied slot carries no field the event did not', () => {
    const result = applySlotChange([slot()], change(), DATE);

    expect(result.kind === 'applied' && Object.keys(result.slots[0]).sort()).toEqual(
      Object.keys(slot()).sort(),
    );
  });
});

describe('mergeBookingResponse', () => {
  it('restores myBookingId when the caller’s own event arrived before its HTTP response', () => {
    const afterOwnEvent = applySlotChange([slot()], change({ sequence: 11 }), DATE);
    const held = afterOwnEvent.kind === 'applied' ? afterOwnEvent.slots : [];

    const merged = mergeBookingResponse(held, booking({ sequence: 11 }));

    expect(merged[0]).toEqual(slot({ isBooked: true, myBookingId: 'b1', sequence: 11 }));
  });

  it('ignores a response older than what is held: the slot changed hands afterwards', () => {
    const held = [slot({ isBooked: false, sequence: 12 })];

    expect(mergeBookingResponse(held, booking({ sequence: 11 }))).toEqual(held);
  });
});

describe('mergeSnapshot', () => {
  it('takes the snapshot where it is at least as new', () => {
    const held = [slot({ sequence: 10 })];
    const snapshot = [slot({ isBooked: true, myBookingId: 'b1', sequence: 11 })];

    expect(mergeSnapshot(held, snapshot)).toEqual(snapshot);
  });

  it('keeps an event that raced ahead of the snapshot', () => {
    const held = [slot({ isBooked: true, sequence: 12 })];
    const snapshot = [slot({ isBooked: false, sequence: 11 })];

    expect(mergeSnapshot(held, snapshot)).toEqual(held);
  });

  it('drops slots the snapshot no longer contains', () => {
    const held = [slot(), slot({ slotId: 'gone' })];

    expect(mergeSnapshot(held, [slot()]).map((s) => s.slotId)).toEqual(['s1']);
  });
});

describe('slotStatus', () => {
  const beforeStart = new Date(`${DATE}T08:00:00Z`);

  it('derives free, booked and mine before the slot starts', () => {
    expect(slotStatus(slot(), beforeStart)).toBe('free');
    expect(slotStatus(slot({ isBooked: true }), beforeStart)).toBe('booked');
    expect(slotStatus(slot({ isBooked: true, myBookingId: 'b1' }), beforeStart)).toBe('mine');
  });

  it('is past once the slot has started, whoever holds it', () => {
    expect(slotStatus(slot({ myBookingId: 'b1' }), new Date(`${DATE}T09:00:00Z`))).toBe('past');
  });
});
