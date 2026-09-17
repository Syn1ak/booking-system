import { IBooking } from '../../../../../core/entities/bookings/booking.dto';
import { ISlot } from '../../../../../core/entities/rooms/room-schedule.dto';
import { ISlotChange } from '../../../../../core/entities/realtime/slot-change.dto';
import { utcDateOf } from '../../../../../core/utils/date/utc-date.util';
import { TSlotChangeResult, TSlotStatus } from '../models/slot.types';

/*
 * The client half of the real-time contract in .claude/realtime/realtime.md, "What a client
 * may conclude from an event". Every write to slot state goes through these functions.
 * Sequences are only compared, never used in arithmetic.
 */

/**
 * A snapshot wins wherever it is at least as new as what is held, so an event that raced ahead
 * of the fetch survives it. Slots absent from the snapshot are dropped: the grid changed.
 */
export function mergeSnapshot(held: readonly ISlot[], snapshot: readonly ISlot[]): ISlot[] {
  const heldById = new Map(held.map((slot) => [slot.slotId, slot]));

  return snapshot.map((incoming) => {
    const current = heldById.get(incoming.slotId);
    return current && current.sequence > incoming.sequence ? current : incoming;
  });
}

/**
 * The caller's own claim. An equal sequence means the pushed event for this very booking was
 * applied first and cleared `myBookingId`; the response restores it. A lower one means the slot
 * has changed hands since, and the response is stale.
 */
export function mergeBookingResponse(held: readonly ISlot[], booking: IBooking): ISlot[] {
  return held.map((slot) =>
    slot.slotId === booking.slotId && booking.sequence >= slot.sequence
      ? { ...slot, isBooked: true, myBookingId: booking.bookingId, sequence: booking.sequence }
      : slot,
  );
}

/**
 * An event names no holder, so a newer one always clears `myBookingId`: whatever happened after
 * the version the caller held, the slot is no longer known to be theirs.
 */
export function applySlotChange(
  held: readonly ISlot[],
  change: ISlotChange,
  viewedDate: string,
): TSlotChangeResult {
  if (utcDateOf(change.startsAtUtc) !== viewedDate) {
    return { kind: 'ignored' };
  }

  const current = held.find((slot) => slot.slotId === change.slotId);

  if (!current) {
    return { kind: 'unreconcilable' };
  }

  if (change.sequence <= current.sequence) {
    return { kind: 'ignored' };
  }

  return {
    kind: 'applied',
    slots: held.map((slot) =>
      slot === current
        ? { ...slot, isBooked: change.isBooked, myBookingId: null, sequence: change.sequence }
        : slot,
    ),
  };
}

export function slotStatus(slot: ISlot, now: Date): TSlotStatus {
  if (new Date(slot.startsAtUtc).getTime() <= now.getTime()) {
    return 'past';
  }

  if (slot.myBookingId) {
    return 'mine';
  }

  return slot.isBooked ? 'booked' : 'free';
}
